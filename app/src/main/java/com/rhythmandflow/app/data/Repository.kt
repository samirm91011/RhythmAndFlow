package com.rhythmandflow.app.data

import android.util.Log
import com.google.gson.Gson
import com.google.gson.JsonParser
import java.io.IOException
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.asSharedFlow
import retrofit2.HttpException
import retrofit2.Response

/** Result of a network call with a message that is safe to show to the user. */
sealed class Outcome<out T> {
    data class Ok<T>(val value: T) : Outcome<T>()
    /** raw is the server's error body, for the few places that need more than the message (for example the email to confirm). */
    data class Fail(val message: String, val code: Int? = null, val raw: String? = null) : Outcome<Nothing>()
}

inline fun <T, R> Outcome<T>.map(f: (T) -> R): Outcome<R> = when (this) {
    is Outcome.Ok -> Outcome.Ok(f(value))
    is Outcome.Fail -> this
}

/** Where unexpected problems are sent so the team hears about them (see diagnostics/ErrorReporter). */
interface ErrorSink {
    fun report(message: String, details: String?, route: String?, fatal: Boolean)
}

class Repository(private val api: Api, private val tokens: TokenStore, private val errors: ErrorSink) {
    private val _unauthorized = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    /** Emits when the server rejects our token (expired/invalid) so the app can sign the user out. */
    val unauthorized: SharedFlow<Unit> = _unauthorized.asSharedFlow()

    val hasToken get() = tokens.token != null

    private suspend fun <T> call(block: suspend () -> T): Outcome<T> = try {
        Outcome.Ok(block())
    } catch (e: CancellationException) {
        throw e                                   // the screen was closed: not an error
    } catch (e: HttpException) {
        val code = e.code()
        val raw = e.response()?.errorBody()?.string()
        val msg = errorMessage(raw, code)
        if (code == 401 && hasToken) _unauthorized.tryEmit(Unit)
        if (code >= 500) errors.report("Server error $code on ${e.response()?.raw()?.request?.url?.encodedPath}", msg, null, false)
        Outcome.Fail(msg, code, raw)
    } catch (e: IOException) {
        Outcome.Fail("Can't reach the Rhythm & Flow server. Check your internet connection and try again.")
    } catch (e: Exception) {
        // Something we did not expect (for example the server sent data in a shape the app can't read): tell the team.
        errors.report("${e.javaClass.simpleName}: ${e.message}", Log.getStackTraceString(e), null, false)
        Outcome.Fail("Something went wrong. Please try again.")
    }

    private suspend fun callUnit(block: suspend () -> Response<Unit>): Outcome<Unit> = try {
        val r = block()
        if (r.isSuccessful) Outcome.Ok(Unit) else {
            if (r.code() == 401 && hasToken) _unauthorized.tryEmit(Unit)
            val msg = errorMessage(r.errorBody()?.string(), r.code())
            if (r.code() >= 500) errors.report("Server error ${r.code()} on ${r.raw().request.url.encodedPath}", msg, null, false)
            Outcome.Fail(msg, r.code())
        }
    } catch (e: CancellationException) {
        throw e
    } catch (e: HttpException) {
        Outcome.Fail(errorMessage(e.response()?.errorBody()?.string(), e.code()), e.code())
    } catch (e: IOException) {
        Outcome.Fail("Can't reach the Rhythm & Flow server. Check your internet connection and try again.")
    } catch (e: Exception) {
        errors.report("${e.javaClass.simpleName}: ${e.message}", Log.getStackTraceString(e), null, false)
        Outcome.Fail("Something went wrong. Please try again.")
    }
    private fun errorMessage(body: String?, code: Int): String = friendlyErrorMessage(body, code)

    // ---- Auth ----
    suspend fun login(identifier: String, password: String) = call { api.login(LoginRequest(identifier, password)) }
        .also { if (it is Outcome.Ok) tokens.token = it.value.token }
    /** Sign-up does not sign anyone in: the person confirms their email with an emailed code first. */
    suspend fun register(name: String, username: String, email: String, password: String) =
        call { api.register(RegisterRequest(name, username, email, password)) }
    suspend fun verifyEmail(email: String, code: String) = call { api.verifyEmail(VerifyEmailRequest(email.trim(), code.trim())) }
    suspend fun resendVerification(email: String) = call { api.resendVerification(ResendVerificationRequest(email.trim())) }
    suspend fun forgotPassword(email: String) = call { api.forgotPassword(ForgotPasswordRequest(email.trim())) }
    suspend fun resetPassword(email: String, code: String, newPassword: String) =
        call { api.resetPassword(ResetPasswordRequest(email.trim(), code.trim(), newPassword)) }
    /** Changing the password signs other devices out, so the server returns a fresh token for this one. */
    suspend fun changePassword(current: String, new: String) = call { api.changePassword(ChangePasswordRequest(current, new)) }
        .also { if (it is Outcome.Ok) tokens.token = it.value.token }
    suspend fun registerDeviceToken(token: String) = call { api.registerDeviceToken(DeviceTokenRequest(token)) }
    suspend fun removeDeviceToken(token: String) = call { api.removeDeviceToken(DeviceTokenRequest(token)) }
    suspend fun payments() = call { api.payments() }
    suspend fun adminUsers(search: String?) = call { api.adminUsers(search?.takeIf { it.isNotBlank() }) }
    suspend fun adminSetUserStatus(id: String, status: String) = callUnit { api.adminSetUserStatus(id, SetUserStatusRequest(status)) }
    suspend fun exportData() = call { api.exportData().use { it.string() } }
    suspend fun deleteAccount(password: String) = call { api.deleteAccount(DeleteAccountRequest(password)) }
    suspend fun me() = call { api.me() }
    suspend fun updateProfile(name: String, email: String, about: String?) = call { api.updateProfile(UpdateProfileRequest(name, email, about)) }
    fun signOut() = tokens.clear()

    // ---- Plans & subscriptions ----
    suspend fun plans() = call { api.plans() }
    suspend fun subscriptions() = call { api.subscriptions() }
    suspend fun checkout(planId: Int, billing: String? = null) = call { api.checkout(CheckoutRequest(planId, billing)) }
    suspend fun changePreview(subId: Int, planId: Int, billing: String?) = call { api.changePreview(subId, planId, billing) }
    suspend fun buyTicket(classId: Int, useCredit: Boolean = true) = call { api.buyTicket(classId, TicketRequest(useCredit)) }
    suspend fun ticketQuote(classId: Int) = call { api.ticketQuote(classId) }
    suspend fun joinWaitlist(classId: Int) = callUnit { api.joinWaitlist(classId) }
    suspend fun cancelTicket(id: Int, choice: String?, note: String?) = call { api.cancelTicket(id, CancelRequestBody(choice, note)) }
    suspend fun chooseAfterChange(id: Int, choice: String) = call { api.chooseAfterChange(id, ChoiceBody(choice)) }
    suspend fun reportIssue(id: Int, description: String) = call { api.reportIssue(id, ReportIssueBody(description)) }
    suspend fun credits() = call { api.credits() }
    suspend fun changePlan(subId: Int, planId: Int, billing: String?) = call { api.changePlan(subId, CheckoutRequest(planId, billing)) }
    suspend fun policies() = call { api.policies() }
    suspend fun cancelSubscription(id: Int) = call { api.cancelSubscription(id) }
    suspend fun simulatePayment(id: Int) = callUnit { api.simulatePayment(id) }

    // ---- Content ----
    suspend fun lessons(category: String? = null, query: String? = null, programmeId: Int? = null) =
        call { api.lessons(category?.takeIf { it != "All" }, query?.takeIf { it.isNotBlank() }, programmeId) }
    suspend fun programmes() = call { api.programmes() }
    suspend fun adminProgrammes() = call { api.adminProgrammes() }
    suspend fun adminCreateProgramme(p: ProgrammeUpsert) = call { api.adminCreateProgramme(p) }
    suspend fun adminUpdateProgramme(id: Int, p: ProgrammeUpsert) = callUnit { api.adminUpdateProgramme(id, p) }
    suspend fun lesson(id: Int) = call { api.lesson(id) }
    suspend fun playback(id: Int) = call { api.playback(id) }
    suspend fun updateProgress(lessonId: Int, seconds: Int) = call { api.updateProgress(ProgressUpdate(lessonId, seconds)) }
    suspend fun progressSummary() = call { api.progressSummary() }

    // ---- Classes ----
    suspend fun classes() = call { api.classes() }
    suspend fun bookings() = call { api.bookings() }
    suspend fun book(classId: Int) = call { api.book(classId) }
    suspend fun cancelBooking(id: Int) = callUnit { api.cancelBooking(id) }

    // ---- Notifications ----
    suspend fun notifications(afterId: Int? = null, unreadOnly: Boolean = false) = call { api.notifications(afterId, unreadOnly) }
    suspend fun unreadCount() = call { api.unreadCount() }
    suspend fun markRead(ids: List<Int>?) = callUnit { api.markRead(MarkReadRequest(ids)) }

    // ---- Journal ----
    suspend fun journal() = call { api.journal() }
    suspend fun addJournal(kind: String, mood: String?, text: String?) = call { api.addJournal(JournalRequest(kind, mood, text)) }
    suspend fun deleteJournal(id: Int) = callUnit { api.deleteJournal(id) }

    // ---- Admin ----
    suspend fun adminSummary() = call { api.adminSummary() }
    suspend fun adminErrors(status: String) = call { api.adminErrors(status) }
    suspend fun adminResolveError(id: Int) = callUnit { api.adminResolveError(id) }
    suspend fun adminResolveAllErrors() = callUnit { api.adminResolveAllErrors() }
    suspend fun adminDeleteError(id: Int) = callUnit { api.adminDeleteError(id) }
    suspend fun adminClearResolvedErrors() = callUnit { api.adminClearResolvedErrors() }
    suspend fun adminDeleteClass(id: Int) = callUnit { api.adminDeleteClass(id) }
    suspend fun adminClearFinishedClasses() = call { api.adminClearFinishedClasses() }
    suspend fun adminDeleteUser(id: String) = callUnit { api.adminDeleteUser(id) }
    suspend fun adminDeleteProgramme(id: Int) = callUnit { api.adminDeleteProgramme(id) }
    suspend fun adminDeletePlan(id: Int) = callUnit { api.adminDeletePlan(id) }
    suspend fun adminClasses() = call { api.adminClasses() }
    suspend fun adminBookings() = call { api.adminBookings() }
    suspend fun adminSubscriptions() = call { api.adminSubscriptions() }
    suspend fun adminAttendees(classId: Int) = call { api.adminAttendees(classId) }
    suspend fun adminCreateClass(c: ClassUpsert) = call { api.adminCreateClass(c) }
    suspend fun adminEventRequests() = call { api.adminEventRequests() }
    suspend fun adminResolveRequest(id: Int, approve: Boolean, amount: Double?) = callUnit { api.adminResolveRequest(id, ResolveRequestBody(approve, amount)) }
    suspend fun adminRefunds() = call { api.adminRefunds() }
    suspend fun adminMarkRefunded(id: Int) = callUnit { api.adminMarkRefunded(id) }
    suspend fun suggestion(mood: String) = call { api.suggestion(mood) }
    suspend fun feelInsight() = call { api.feelInsight() }
    suspend fun content() = call { api.content().orBuiltIn() }
    suspend fun adminContent() = call { api.adminContent().orBuiltIn() }
    suspend fun adminSaveContent(c: AppContent) = callUnit { api.adminSaveContent(c) }
    suspend fun adminResetContent() = callUnit { api.adminResetContent() }
    suspend fun adminVideoUploadUrl(fileName: String, contentType: String, sizeBytes: Long) = call { api.adminVideoUploadUrl(VideoUploadBody(fileName, contentType, sizeBytes)) }
    suspend fun adminGiveCredit(id: Int, amount: Double, reason: String?) = callUnit { api.adminGiveCredit(id, GiveCreditBody(amount, reason)) }
    suspend fun adminSendLinks(id: Int) = callUnit { api.adminSendLinks(id) }
    suspend fun adminCancelClass(id: Int) = callUnit { api.adminCancelClass(id) }
    suspend fun adminCreateLesson(l: LessonUpsert) = call { api.adminCreateLesson(l) }
    suspend fun adminLessons() = call { api.adminLessons() }
    suspend fun adminUpdateLesson(id: Int, l: LessonUpsert) = callUnit { api.adminUpdateLesson(id, l) }
    suspend fun adminUpdateClass(id: Int, c: ClassUpsert) = callUnit { api.adminUpdateClass(id, c) }
    suspend fun adminDeleteLesson(id: Int) = callUnit { api.adminDeleteLesson(id) }
    suspend fun adminPlans() = call { api.adminPlans() }
    suspend fun adminCreatePlan(p: PlanUpsert) = call { api.adminCreatePlan(p) }
    suspend fun adminUpdatePlan(id: Int, p: PlanUpsert) = callUnit { api.adminUpdatePlan(id, p) }

    @Suppress("unused") private val gson = Gson()
}
