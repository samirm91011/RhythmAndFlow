package com.rhythmandflow.app.data

// Field names match the JSON produced by the ASP.NET Core API (camelCase).

data class User(
    val id: String,
    val fullName: String,
    val username: String,
    val email: String,
    val role: String,
    val about: String,
) {
    val isAdmin get() = role == "ADMIN"
}

data class AuthResponse(val token: String, val user: User)
data class RegisterRequest(val fullName: String, val username: String, val email: String, val password: String)
data class LoginRequest(val identifier: String, val password: String)
data class UpdateProfileRequest(val fullName: String, val email: String, val about: String?)
data class ForgotPasswordRequest(val email: String)
data class ResetPasswordRequest(val email: String, val code: String, val newPassword: String)
data class ChangePasswordRequest(val currentPassword: String, val newPassword: String)
data class MessageResponse(val message: String?)
data class DeleteAccountRequest(val password: String)

data class Plan(
    val id: Int,
    val name: String,
    val description: String,
    val price: Double,
    val billingFrequency: String,
    val tier: Int,
    val features: List<String>,
)

data class Subscription(
    val id: Int,
    val planId: Int,
    val planName: String,
    val tier: Int,
    val price: Double,
    val status: String,
    val startDate: String?,
    val endDate: String?,
    val grantsAccess: Boolean,
)

data class CheckoutRequest(val planId: Int)
data class CancelResult(val message: String, val cancelledWithPayFast: Boolean)
data class CheckoutResponse(val subscriptionId: Int, val paymentUrl: String)

data class Programme(val id: Int, val name: String, val description: String, val minTier: Int, val locked: Boolean, val lessonCount: Int)

data class Lesson(
    val id: Int,
    val programmeId: Int,
    val programmeName: String,
    val title: String,
    val description: String,
    val category: String,
    val level: String,
    val durationSeconds: Int,
    val thumbnailUrl: String?,
    val isPreview: Boolean,
    val locked: Boolean,
    val completionPercentage: Double,
    val watchTimeSeconds: Int,
) {
    val durationLabel: String
        get() = if (durationSeconds < 90) "${durationSeconds} secs" else "${(durationSeconds + 30) / 60} mins"
}

data class Playback(val url: String, val expiresAt: String, val resumeSeconds: Int)
data class ProgressUpdate(val lessonId: Int, val watchTimeSeconds: Int)
data class ProgressResult(val lessonId: Int, val watchTimeSeconds: Int, val completionPercentage: Double, val completed: Boolean)
data class ProgressSummary(val completedLessons: Int, val inProgressLessons: Int, val sessionsThisMonth: Int, val minutesWatched: Int)

data class ClassItem(
    val id: Int,
    val name: String,
    val description: String,
    val coachName: String,
    val location: String,
    val startTime: String,
    val endTime: String,
    val capacity: Int,
    val spotsLeft: Int,
    val bookedByMe: Boolean,
    val status: String,
)

data class Booking(
    val id: Int,
    val classId: Int,
    val className: String,
    val coachName: String,
    val location: String,
    val startTime: String,
    val endTime: String,
    val status: String,
    val canCancel: Boolean,
)

data class AppNotification(val id: Int, val kind: String, val title: String, val body: String, val route: String?, val createdAt: String, val read: Boolean)
data class MarkReadRequest(val ids: List<Int>?)
data class UnreadCount(val count: Int)

data class JournalRequest(val kind: String, val mood: String?, val text: String?)
data class JournalEntry(val id: Int, val kind: String, val mood: String, val text: String, val createdAt: String)

data class AdminSummary(
    val users: Int,
    val activeSubscriptions: Int,
    val upcomingClasses: Int,
    val activeBookings: Int,
    val monthlyRecurringRevenue: Double,
    val openErrors: Int = 0,
)

data class ErrorReportBody(
    val message: String, val details: String?, val route: String?, val appVersion: String?, val device: String?, val fatal: Boolean,
)

data class ErrorLogItem(
    val id: Int, val source: String, val message: String, val details: String, val route: String?, val userEmail: String?,
    val appVersion: String?, val device: String?, val count: Int, val firstSeen: String, val lastSeen: String, val status: String,
)

data class LessonUpsert(
    val programmeId: Int,
    val title: String,
    val description: String?,
    val category: String,
    val level: String?,
    val durationSeconds: Int,
    val videoProvider: String,
    val videoReference: String,
    val isPreview: Boolean,
)

data class ClassUpsert(
    val name: String,
    val description: String?,
    val coachName: String,
    val location: String,
    val startTime: String,
    val endTime: String,
    val capacity: Int,
)

data class PlanUpsert(val name: String, val description: String?, val price: Double, val tier: Int, val features: String?, val status: String?)
