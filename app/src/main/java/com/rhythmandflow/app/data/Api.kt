package com.rhythmandflow.app.data

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.PUT
import retrofit2.http.Path
import retrofit2.http.Query

interface Api {
    // ---- Auth ----
    @POST("api/auth/register") suspend fun register(@Body body: RegisterRequest): RegisterResponse
    @POST("api/auth/verify-email") suspend fun verifyEmail(@Body body: VerifyEmailRequest): MessageResponse
    @POST("api/auth/resend-verification") suspend fun resendVerification(@Body body: ResendVerificationRequest): MessageResponse
    @POST("api/auth/login") suspend fun login(@Body body: LoginRequest): AuthResponse
    @GET("api/auth/me") suspend fun me(): User
    @PUT("api/auth/me") suspend fun updateProfile(@Body body: UpdateProfileRequest): User
    @POST("api/auth/forgot-password") suspend fun forgotPassword(@Body body: ForgotPasswordRequest): MessageResponse
    @POST("api/auth/reset-password") suspend fun resetPassword(@Body body: ResetPasswordRequest): MessageResponse
    @POST("api/auth/change-password") suspend fun changePassword(@Body body: ChangePasswordRequest): AuthResponse
    @POST("api/notifications/device-token") suspend fun registerDeviceToken(@Body body: DeviceTokenRequest): MessageResponse
    @POST("api/notifications/device-token/remove") suspend fun removeDeviceToken(@Body body: DeviceTokenRequest): MessageResponse
    @GET("api/subscriptions/payments") suspend fun payments(): List<PaymentItem>
    @GET("api/admin/programmes") suspend fun adminProgrammes(): List<AdminProgramme>
    @POST("api/admin/programmes") suspend fun adminCreateProgramme(@Body body: ProgrammeUpsert): Int
    @PUT("api/admin/programmes/{id}") suspend fun adminUpdateProgramme(@Path("id") id: Int, @Body body: ProgrammeUpsert): Response<Unit>
    @GET("api/admin/users") suspend fun adminUsers(@Query("search") search: String? = null): List<AdminUser>
    @POST("api/admin/users/{id}/status") suspend fun adminSetUserStatus(@Path("id") id: String, @Body body: SetUserStatusRequest): Response<Unit>
    @GET("api/account/export") suspend fun exportData(): okhttp3.ResponseBody
    @POST("api/account/delete") suspend fun deleteAccount(@Body body: DeleteAccountRequest): MessageResponse

    // ---- Plans & subscriptions ----
    @GET("api/plans") suspend fun plans(): List<Plan>
    @GET("api/subscriptions") suspend fun subscriptions(): List<Subscription>
    @POST("api/subscriptions/checkout") suspend fun checkout(@Body body: CheckoutRequest): CheckoutResponse
    @POST("api/subscriptions/{id}/cancel") suspend fun cancelSubscription(@Path("id") id: Int): CancelResult
    @GET("api/subscriptions/{id}/change-preview")
    suspend fun changePreview(@Path("id") id: Int, @Query("planId") planId: Int, @Query("billing") billing: String?): PlanChangePreview
    @POST("api/subscriptions/{id}/change") suspend fun changePlan(@Path("id") id: Int, @Body body: CheckoutRequest): CheckoutResponse
    @GET("api/policies") suspend fun policies(): List<Policy>
    @POST("api/dev/simulate-payment/{id}") suspend fun simulatePayment(@Path("id") id: Int): Response<Unit>

    // ---- Content ----
    @GET("api/programmes") suspend fun programmes(): List<Programme>
    @GET("api/lessons") suspend fun lessons(
        @Query("category") category: String? = null,
        @Query("q") query: String? = null,
        @Query("programmeId") programmeId: Int? = null,
    ): List<Lesson>
    @GET("api/lessons/{id}") suspend fun lesson(@Path("id") id: Int): Lesson
    @GET("api/lessons/{id}/playback") suspend fun playback(@Path("id") id: Int): Playback
    @POST("api/progress") suspend fun updateProgress(@Body body: ProgressUpdate): ProgressResult
    @GET("api/progress/summary") suspend fun progressSummary(): ProgressSummary

    // ---- Classes & bookings ----
    @GET("api/classes") suspend fun classes(): List<ClassItem>
    @GET("api/bookings") suspend fun bookings(): List<Booking>
    @POST("api/classes/{id}/book") suspend fun book(@Path("id") id: Int): Booking
    @POST("api/bookings/{id}/cancel") suspend fun cancelBooking(@Path("id") id: Int): Response<Unit>
    @POST("api/classes/{id}/ticket") suspend fun buyTicket(@Path("id") id: Int, @Body body: TicketRequest): TicketResponse
    @GET("api/classes/{id}/quote") suspend fun ticketQuote(@Path("id") id: Int): TicketQuote
    @POST("api/classes/{id}/waitlist") suspend fun joinWaitlist(@Path("id") id: Int): Response<Unit>
    @POST("api/bookings/{id}/cancel-ticket") suspend fun cancelTicket(@Path("id") id: Int, @Body body: CancelRequestBody): ActionResult
    @POST("api/bookings/{id}/choice") suspend fun chooseAfterChange(@Path("id") id: Int, @Body body: ChoiceBody): ActionResult
    @POST("api/bookings/{id}/report-issue") suspend fun reportIssue(@Path("id") id: Int, @Body body: ReportIssueBody): ActionResult
    @GET("api/credits") suspend fun credits(): Credits

    // ---- Notifications ----
    @GET("api/notifications") suspend fun notifications(
        @Query("afterId") afterId: Int? = null,
        @Query("unreadOnly") unreadOnly: Boolean = false,
    ): List<AppNotification>
    @GET("api/notifications/unread-count") suspend fun unreadCount(): UnreadCount
    @POST("api/notifications/read") suspend fun markRead(@Body body: MarkReadRequest): Response<Unit>

    // ---- Journal ----
    @GET("api/journal") suspend fun journal(): List<JournalEntry>
    @POST("api/journal") suspend fun addJournal(@Body body: JournalRequest): JournalEntry
    @DELETE("api/journal/{id}") suspend fun deleteJournal(@Path("id") id: Int): Response<Unit>

    // ---- Error reporting ----
    @POST("api/telemetry/errors") suspend fun reportError(@Body body: ErrorReportBody): Response<Unit>

    // ---- Admin ----
    @GET("api/admin/errors") suspend fun adminErrors(@Query("status") status: String = "NEW"): List<ErrorLogItem>
    @POST("api/admin/errors/{id}/resolve") suspend fun adminResolveError(@Path("id") id: Int): Response<Unit>
    @POST("api/admin/errors/resolve-all") suspend fun adminResolveAllErrors(): Response<Unit>
    @GET("api/admin/summary") suspend fun adminSummary(): AdminSummary
    @GET("api/admin/lessons") suspend fun adminLessons(): List<AdminLesson>
    @PUT("api/admin/lessons/{id}") suspend fun adminUpdateLesson(@Path("id") id: Int, @Body body: LessonUpsert): Response<Unit>
    @PUT("api/admin/classes/{id}") suspend fun adminUpdateClass(@Path("id") id: Int, @Body body: ClassUpsert): Response<Unit>
    @POST("api/admin/lessons") suspend fun adminCreateLesson(@Body body: LessonUpsert): Int
    @DELETE("api/admin/lessons/{id}") suspend fun adminDeleteLesson(@Path("id") id: Int): Response<Unit>
    @GET("api/admin/bookings") suspend fun adminBookings(): List<AdminBooking>
    @GET("api/admin/subscriptions") suspend fun adminSubscriptions(): List<AdminSubscription>
    @GET("api/admin/classes/{id}/attendees") suspend fun adminAttendees(@Path("id") id: Int): List<AdminAttendee>
    @GET("api/admin/classes") suspend fun adminClasses(): List<ClassItem>
    @POST("api/admin/classes") suspend fun adminCreateClass(@Body body: ClassUpsert): Int
    @POST("api/admin/classes/{id}/cancel") suspend fun adminCancelClass(@Path("id") id: Int): Response<Unit>
    @GET("api/admin/event-requests") suspend fun adminEventRequests(): List<AdminEventRequest>
    @POST("api/admin/bookings/{id}/resolve") suspend fun adminResolveRequest(@Path("id") id: Int, @Body body: ResolveRequestBody): Response<Unit>
    @GET("api/admin/refunds") suspend fun adminRefunds(): List<AdminRefund>
    @POST("api/admin/bookings/{id}/refunded") suspend fun adminMarkRefunded(@Path("id") id: Int): Response<Unit>
    @POST("api/admin/bookings/{id}/credit") suspend fun adminGiveCredit(@Path("id") id: Int, @Body body: GiveCreditBody): Response<Unit>
    @POST("api/admin/classes/{id}/send-links") suspend fun adminSendLinks(@Path("id") id: Int): Response<Unit>
    @GET("api/admin/plans") suspend fun adminPlans(): List<AdminPlan>
    @POST("api/admin/plans") suspend fun adminCreatePlan(@Body body: PlanUpsert): Int
    @PUT("api/admin/plans/{id}") suspend fun adminUpdatePlan(@Path("id") id: Int, @Body body: PlanUpsert): Response<Unit>
}
