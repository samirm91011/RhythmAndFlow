package com.rhythmandflow.app.data.demo

import com.rhythmandflow.app.data.*
import java.time.Duration
import java.time.Instant
import kotlinx.coroutines.delay
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import retrofit2.HttpException
import retrofit2.Response

/**
 * Built-in sample data so the whole app can run with no server (switch on with  gradlew assembleDebug -PdemoMode=true).
 * Used for screen-by-screen visual testing and as an emergency fallback for a presentation. Off in every normal build.
 * Any password works; a username containing "admin" signs in as an administrator. Reset code: 123456.
 */
class DemoApi : Api {
    private var user = User("demo-user", "Alex Demo", "alex", "alex@rhythmandflow.test", "CUSTOMER", "A work in progress, and that's enough.")
    private var tier = 0
    private var sub: Subscription? = null
    private var checkoutPolls = 0
    private var nextId = 100

    private val plans = mutableListOf(
        Plan(1, "Flow", "Start moving with the essentials.", 99.0, "MONTHLY", 1, listOf("Move & Release programme", "Dance with Joy programme", "Progress tracking", "Class booking")),
        Plan(2, "Rhythm", "Everything in Flow plus deeper practices.", 199.0, "MONTHLY", 2, listOf("Everything in Flow", "Mindful Mobility programme", "Meditation library", "Priority class booking")),
        Plan(3, "Rhythm & Flow Unlimited", "The full Rhythm & Flow experience.", 299.0, "MONTHLY", 3, listOf("Everything in Rhythm", "Full Body Flow masterclasses", "New content first", "Mentorship Q&A access")),
    )

    private data class L(val id: Int, val prog: Int, val progName: String, val minTier: Int, val title: String, val desc: String, val cat: String, val secs: Int, val preview: Boolean)
    private val lessonsData = listOf(
        L(1, 1, "Move & Release", 1, "Full Body Flow", "A gentle flow to move, breathe and reconnect with your body.", "Yoga", 600, true),
        L(2, 1, "Move & Release", 1, "Move & Release", "Release tension and feel lighter.", "Stretch", 900, false),
        L(3, 2, "Dance with Joy", 1, "Dance with Joy", "Feel-good movement for your mood.", "Dance", 1500, true),
        L(4, 2, "Dance with Joy", 1, "Barre Basics", "Build strength and grace at the barre.", "Barre", 1200, false),
        L(5, 3, "Mindful Mobility", 2, "Gentle Mobility", "Ease into stretching and mobility work.", "Stretch", 900, false),
        L(6, 3, "Mindful Mobility", 2, "6-Minute Reset", "A short guided meditation to breathe and reset.", "Meditation", 360, false),
        L(7, 4, "Full Body Flow Masterclass", 3, "Sunrise Flow", "A longer morning flow to start your day.", "Yoga", 2100, false),
        L(8, 4, "Full Body Flow Masterclass", 3, "Dance Cardio Burn", "High energy dance for a full body workout.", "Dance", 1800, false),
    )
    private val watched = mutableMapOf(1 to 240)   // Alex is part-way through the first lesson, so Home shows "continue"

    private val classes = mutableListOf(
        mkClass(1, "Morning Flow", 1, 7, 20, 3), mkClass(2, "Dance with Joy", 2, 12, 20, 12), mkClass(3, "Barre & Stretch", 3, 17, 20, 0),
        mkClass(4, "Yoga Reset", 4, 8, 15, 15), mkClass(5, "Weekend Wind-Down", 5, 9, 20, 18), mkClass(6, "Mobility for Life", 6, 10, 20, 20),
    )
    private val booked = mutableMapOf<Int, Int>()          // bookingId -> classId
    private val journal = mutableListOf(
        JournalEntry(1, "REFLECTION", "Calmer", "Felt tight this morning, a slow flow helped a lot.", ago(26 * 60)),
        JournalEntry(2, "GRATITUDE", "", "My body for carrying me through the week.", ago(50 * 60)),
    )
    private val notifications = mutableListOf(
        AppNotification(3, "CLASS", "Class cancelled", "Sorry, Barre & Stretch on Fri 5 Oct has been cancelled.", "classes", ago(12), false),
        AppNotification(2, "PAYMENT", "Welcome to Rhythm & Flow", "Subscribe to unlock every programme.", "plans", ago(180), true),
    )
    private val errors = mutableListOf(
        ErrorLogItem(3, "APP", "[CRASH] NullPointerException: lesson was null", "java.lang.NullPointerException\n  at com.rhythmandflow.app.Player.play(Player.kt:42)\n  at ...", "app crash", "alex@rhythmandflow.test", "Pixel 8 (Android 15)", "1.0", 2, ago(240), ago(35), "NEW"),
        ErrorLogItem(2, "API", "Server error 500 on /api/lessons/9/playback", "System.InvalidOperationException: Sequence contains no elements", "GET /api/lessons/9/playback", null, null, null, 1, ago(600), ago(600), "NEW"),
        ErrorLogItem(1, "APP", "SocketTimeoutException: timeout", "okhttp3 ...", "home", "sam@example.com", "Galaxy A14 (Android 14)", "1.0", 5, ago(2000), ago(900), "RESOLVED"),
    )

    // ---- helpers
    private suspend fun pause() = delay(180)
    private fun ago(minutes: Long) = Instant.now().minus(Duration.ofMinutes(minutes)).toString()
    private fun fail(code: Int, message: String): Nothing =
        throw HttpException(Response.error<Any>(code, "{\"error\":\"$message\"}".toResponseBody("application/json".toMediaType())))
    private fun ok() = Response.success(Unit)
    private fun mkClass(id: Int, name: String, days: Long, hour: Long, capacity: Int, left: Int): ClassItem {
        val start = Instant.now().plus(Duration.ofDays(days)).atZone(java.time.ZoneId.of("Africa/Johannesburg")).withHour(hour.toInt()).withMinute(0).withSecond(0).withNano(0).toInstant()
        return ClassItem(id, name, "All levels welcome. Bring water and a mat.", "Deni", "Rhythm & Flow Studio", start.toString(), start.plus(Duration.ofHours(1)).toString(), capacity, left, false, "SCHEDULED")
    }
    private fun lessonDto(l: L): Lesson {
        val locked = user.role != "ADMIN" && !(l.preview || tier >= l.minTier)
        val w = watched[l.id] ?: 0
        return Lesson(l.id, l.prog, l.progName, l.title, l.desc, l.cat, "All levels", l.secs, null, l.preview, locked, (w * 100.0 / l.secs).coerceAtMost(100.0), w)
    }
    private fun session(admin: Boolean, name: String? = null, email: String? = null, username: String? = null): AuthResponse {
        user = User("demo-user", name ?: if (admin) "Rhythm Admin" else "Alex Demo", username ?: if (admin) "admin" else "alex",
            email ?: if (admin) "admin@rhythmandflow.test" else "alex@rhythmandflow.test", if (admin) "ADMIN" else "CUSTOMER", user.about)
        return AuthResponse("demo-token", user)
    }
    private fun activate(plan: Plan) {
        tier = plan.tier
        val end = Instant.now().plus(Duration.ofDays(30)).toString()
        sub = Subscription(1, plan.id, plan.name, plan.tier, plan.price, "ACTIVE", Instant.now().toString(), end, true)
        notifications.add(0, AppNotification(++nextId, "PAYMENT", "You're subscribed!", "Your ${plan.name} plan is active. Enjoy your practice.", "subscription", ago(0), false))
    }

    // ---- auth
    override suspend fun register(body: RegisterRequest): AuthResponse { pause(); return session(false, body.fullName, body.email, body.username) }
    override suspend fun login(body: LoginRequest): AuthResponse {
        pause()
        if (body.password == "wrong") fail(401, "Incorrect username/email or password.")
        return session(body.identifier.contains("admin", ignoreCase = true))
    }
    override suspend fun me(): User { pause(); return user }
    override suspend fun updateProfile(body: UpdateProfileRequest): User { pause(); user = user.copy(fullName = body.fullName, email = body.email, about = body.about ?: ""); return user }
    override suspend fun forgotPassword(body: ForgotPasswordRequest): MessageResponse { pause(); return MessageResponse("If that email has an account, we've sent a 6-digit code. It expires in 15 minutes.") }
    override suspend fun resetPassword(body: ResetPasswordRequest): MessageResponse {
        pause()
        if (body.code != "123456") fail(400, "That code isn't valid or has expired. Please request a new one.")
        return MessageResponse("Your password has been changed. You can log in now.")
    }
    override suspend fun changePassword(body: ChangePasswordRequest): AuthResponse {
        pause()
        if (body.currentPassword == "wrong") fail(400, "Your current password isn't right.")
        return AuthResponse("demo-token", user)
    }

    override suspend fun registerDeviceToken(body: DeviceTokenRequest): MessageResponse = MessageResponse("Registered.")
    override suspend fun removeDeviceToken(body: DeviceTokenRequest): MessageResponse = MessageResponse("Removed.")
    override suspend fun payments(): List<PaymentItem> {
        pause()
        val s = sub?.takeIf { it.status == "ACTIVE" } ?: return emptyList()
        return listOf(
            PaymentItem(3, s.planName, s.price, "COMPLETE", ago(0), "RF-000003"),
            PaymentItem(2, s.planName, s.price, "COMPLETE", ago(60L * 24 * 30), "RF-000002"),
            PaymentItem(1, s.planName, s.price, "FAILED", ago(60L * 24 * 45), "RF-000001"),
        )
    }
    private val demoUsers = mutableListOf(
        AdminUser("u1", "Alex Demo", "alex", "alex@rhythmandflow.test", "CUSTOMER", "ACTIVE", ago(60L * 24 * 20), "Flow"),
        AdminUser("u2", "Thandi Mokoena", "thandi", "thandi@example.com", "CUSTOMER", "ACTIVE", ago(60L * 24 * 9), "Rhythm"),
        AdminUser("u3", "Sam Naidoo", "sam", "sam@example.com", "CUSTOMER", "ACTIVE", ago(60L * 24 * 3), null),
        AdminUser("u4", "Rhythm Admin", "admin", "admin@rhythmandflow.test", "ADMIN", "ACTIVE", ago(60L * 24 * 60), null),
    )
    override suspend fun adminUsers(search: String?): List<AdminUser> {
        pause()
        val q = search?.trim()?.lowercase().orEmpty()
        return demoUsers.filter { q.isEmpty() || it.fullName.lowercase().contains(q) || it.email.lowercase().contains(q) || it.username.lowercase().contains(q) }
    }
    override suspend fun adminSetUserStatus(id: String, body: SetUserStatusRequest): Response<Unit> {
        pause(); demoUsers.replaceAll { if (it.id == id) it.copy(status = body.status) else it }; return ok()
    }
    override suspend fun exportData(): okhttp3.ResponseBody {
        pause()
        val json = """{"exportedAt":"demo","profile":{"fullName":"${user.fullName}","email":"${user.email}"},"note":"Demo data only."}"""
        return json.toResponseBody("application/json".toMediaType())
    }
    override suspend fun deleteAccount(body: DeleteAccountRequest): MessageResponse {
        pause()
        if (body.password == "wrong") fail(400, "That password isn't right.")
        return MessageResponse("Your account and personal data have been deleted.")
    }

    // ---- plans & subscriptions
    override suspend fun plans(): List<Plan> { pause(); return plans }
    override suspend fun subscriptions(): List<Subscription> {
        pause()
        sub?.let { s -> if (s.status == "PENDING" && ++checkoutPolls >= 2) activate(plans.first { it.id == s.planId }) }
        return listOfNotNull(sub)
    }
    override suspend fun checkout(body: CheckoutRequest): CheckoutResponse {
        pause(); checkoutPolls = 0
        val p = plans.first { it.id == body.planId }
        sub = Subscription(1, p.id, p.name, p.tier, p.price, "PENDING", null, null, false)
        return CheckoutResponse(1, "https://sandbox.payfast.co.za/eng/process?demo=true")
    }
    override suspend fun cancelSubscription(id: Int): CancelResult {
        pause()
        val s = sub ?: fail(409, "No active subscription found.")
        sub = s.copy(status = "CANCELLED")
        return CancelResult("Your subscription is cancelled and PayFast won't charge you again. You keep access until ${s.endDate?.take(10)}.", true)
    }
    override suspend fun simulatePayment(id: Int): Response<Unit> { pause(); sub?.let { activate(plans.first { p -> p.id == it.planId }) }; return ok() }

    // ---- content
    override suspend fun programmes(): List<Programme> = lessonsData.groupBy { it.prog }.map { (id, ls) ->
        Programme(id, ls.first().progName, "", ls.first().minTier, tier < ls.first().minTier, ls.size) }
    override suspend fun lessons(category: String?, query: String?): List<Lesson> {
        pause()
        return lessonsData.filter { (category == null || it.cat == category) && (query.isNullOrBlank() || it.title.contains(query, true) || it.desc.contains(query, true)) }.map(::lessonDto)
    }
    override suspend fun lesson(id: Int): Lesson { pause(); return lessonDto(lessonsData.first { it.id == id }) }
    override suspend fun playback(id: Int): Playback {
        pause()
        if (lessonDto(lessonsData.first { it.id == id }).locked) fail(403, "An active subscription is required to watch this lesson.")
        return Playback("https://storage.googleapis.com/exoplayer-test-media-1/mp4/android-screens-10s.mp4", Instant.now().plus(Duration.ofHours(1)).toString(), 0)
    }
    override suspend fun updateProgress(body: ProgressUpdate): ProgressResult {
        val l = lessonsData.first { it.id == body.lessonId }
        val w = maxOf(watched[l.id] ?: 0, body.watchTimeSeconds).coerceAtMost(l.secs); watched[l.id] = w
        val pct = w * 100.0 / l.secs
        return ProgressResult(l.id, w, pct, pct >= 95)
    }
    override suspend fun progressSummary(): ProgressSummary {
        pause()
        val done = lessonsData.count { (watched[it.id] ?: 0) * 100.0 / it.secs >= 95 }
        return ProgressSummary(done + 3, watched.size - done, 8 + watched.size, 42 + watched.values.sum() / 60)
    }

    // ---- classes & bookings
    private fun classDto(c: ClassItem): ClassItem = c.copy(bookedByMe = booked.containsValue(c.id))
    override suspend fun classes(): List<ClassItem> { pause(); return classes.map(::classDto) }
    private fun bookingDto(id: Int, classId: Int): Booking {
        val c = classes.first { it.id == classId }
        return Booking(id, c.id, c.name, c.coachName, c.location, c.startTime, c.endTime, "BOOKED", true)
    }
    override suspend fun bookings(): List<Booking> { pause(); return booked.map { (id, cid) -> bookingDto(id, cid) } }
    override suspend fun book(id: Int): Booking {
        pause()
        val c = classes.first { it.id == id }
        if (booked.containsValue(id)) fail(409, "You have already booked this class.")
        if (c.spotsLeft == 0) fail(409, "This class is full.")
        classes[classes.indexOf(c)] = c.copy(spotsLeft = c.spotsLeft - 1)
        val bid = ++nextId; booked[bid] = id
        return bookingDto(bid, id)
    }
    override suspend fun cancelBooking(id: Int): Response<Unit> {
        pause()
        val cid = booked.remove(id) ?: return Response.error(409, "{\"error\":\"Booking not found.\"}".toResponseBody("application/json".toMediaType()))
        val c = classes.first { it.id == cid }; classes[classes.indexOf(c)] = c.copy(spotsLeft = c.spotsLeft + 1)
        return ok()
    }

    // ---- notifications
    override suspend fun notifications(afterId: Int?, unreadOnly: Boolean): List<AppNotification> {
        pause(); return notifications.filter { (afterId == null || it.id > afterId) && (!unreadOnly || !it.read) }.sortedByDescending { it.id }
    }
    override suspend fun unreadCount(): UnreadCount { pause(); return UnreadCount(notifications.count { !it.read }) }
    override suspend fun markRead(body: MarkReadRequest): Response<Unit> {
        pause()
        notifications.replaceAll { if (body.ids == null || it.id in body.ids) it.copy(read = true) else it }
        return ok()
    }

    // ---- journal
    override suspend fun journal(): List<JournalEntry> { pause(); return journal.sortedByDescending { it.id } }
    override suspend fun addJournal(body: JournalRequest): JournalEntry {
        pause(); val e = JournalEntry(++nextId, body.kind, body.mood ?: "", body.text ?: "", ago(0)); journal.add(e); return e
    }
    override suspend fun deleteJournal(id: Int): Response<Unit> { pause(); journal.removeAll { it.id == id }; return ok() }

    // ---- error reporting
    override suspend fun reportError(body: ErrorReportBody): Response<Unit> = ok()

    // ---- admin
    override suspend fun adminSummary(): AdminSummary { pause(); return AdminSummary(128, 74, classes.size, booked.size + 41, 74 * 180.0, errors.count { it.status == "NEW" }) }
    override suspend fun adminCreateLesson(body: LessonUpsert): Int { pause(); return ++nextId }
    override suspend fun adminDeleteLesson(id: Int): Response<Unit> { pause(); return ok() }
    override suspend fun adminClasses(): List<ClassItem> { pause(); return classes.map(::classDto) }
    override suspend fun adminCreateClass(body: ClassUpsert): Int { pause(); return ++nextId }
    override suspend fun adminCancelClass(id: Int): Response<Unit> { pause(); return ok() }
    override suspend fun adminPlans(): List<Plan> { pause(); return plans }
    override suspend fun adminUpdatePlan(id: Int, body: PlanUpsert): Response<Unit> {
        pause(); val i = plans.indexOfFirst { it.id == id }; if (i >= 0) plans[i] = plans[i].copy(name = body.name, price = body.price); return ok()
    }
    override suspend fun adminErrors(status: String): List<ErrorLogItem> { pause(); return errors.filter { status == "ALL" || it.status == status } }
    override suspend fun adminResolveError(id: Int): Response<Unit> { pause(); errors.replaceAll { if (it.id == id) it.copy(status = "RESOLVED") else it }; return ok() }
    override suspend fun adminResolveAllErrors(): Response<Unit> { pause(); errors.replaceAll { it.copy(status = "RESOLVED") }; return ok() }
}
