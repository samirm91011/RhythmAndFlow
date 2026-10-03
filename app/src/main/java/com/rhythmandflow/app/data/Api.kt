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
    @POST("api/auth/register") suspend fun register(@Body body: RegisterRequest): AuthResponse
    @POST("api/auth/login") suspend fun login(@Body body: LoginRequest): AuthResponse
    @GET("api/auth/me") suspend fun me(): User
    @PUT("api/auth/me") suspend fun updateProfile(@Body body: UpdateProfileRequest): User
    @POST("api/auth/forgot-password") suspend fun forgotPassword(@Body body: ForgotPasswordRequest): MessageResponse
    @POST("api/auth/reset-password") suspend fun resetPassword(@Body body: ResetPasswordRequest): MessageResponse
    @POST("api/auth/change-password") suspend fun changePassword(@Body body: ChangePasswordRequest): AuthResponse
    @GET("api/account/export") suspend fun exportData(): okhttp3.ResponseBody
    @POST("api/account/delete") suspend fun deleteAccount(@Body body: DeleteAccountRequest): MessageResponse

    // ---- Plans & subscriptions ----
    @GET("api/plans") suspend fun plans(): List<Plan>
    @GET("api/subscriptions") suspend fun subscriptions(): List<Subscription>
    @POST("api/subscriptions/checkout") suspend fun checkout(@Body body: CheckoutRequest): CheckoutResponse
    @POST("api/subscriptions/{id}/cancel") suspend fun cancelSubscription(@Path("id") id: Int): CancelResult
    @POST("api/dev/simulate-payment/{id}") suspend fun simulatePayment(@Path("id") id: Int): Response<Unit>

    // ---- Content ----
    @GET("api/programmes") suspend fun programmes(): List<Programme>
    @GET("api/lessons") suspend fun lessons(
        @Query("category") category: String? = null,
        @Query("q") query: String? = null,
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
    @POST("api/admin/lessons") suspend fun adminCreateLesson(@Body body: LessonUpsert): Int
    @DELETE("api/admin/lessons/{id}") suspend fun adminDeleteLesson(@Path("id") id: Int): Response<Unit>
    @GET("api/admin/classes") suspend fun adminClasses(): List<ClassItem>
    @POST("api/admin/classes") suspend fun adminCreateClass(@Body body: ClassUpsert): Int
    @POST("api/admin/classes/{id}/cancel") suspend fun adminCancelClass(@Path("id") id: Int): Response<Unit>
    @GET("api/admin/plans") suspend fun adminPlans(): List<Plan>
    @PUT("api/admin/plans/{id}") suspend fun adminUpdatePlan(@Path("id") id: Int, @Body body: PlanUpsert): Response<Unit>
}
