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
/** The answer to signing up. There is no token: the person confirms their email, then signs in. */
data class RegisterResponse(val email: String, val verificationRequired: Boolean, val message: String)
data class VerifyEmailRequest(val email: String, val code: String)
data class ResendVerificationRequest(val email: String)
data class LoginRequest(val identifier: String, val password: String)
data class UpdateProfileRequest(val fullName: String, val email: String, val about: String?)
data class ForgotPasswordRequest(val email: String)
data class ResetPasswordRequest(val email: String, val code: String, val newPassword: String)
data class ChangePasswordRequest(val currentPassword: String, val newPassword: String)
data class MessageResponse(val message: String?)
data class DeleteAccountRequest(val password: String)
data class DeviceTokenRequest(val token: String)
data class PaymentItem(val id: Int, val planName: String, val amount: Double, val status: String, val date: String, val receipt: String)
data class AdminUser(val id: String, val fullName: String, val username: String, val email: String, val role: String, val status: String, val createdAt: String, val plan: String?)
data class SetUserStatusRequest(val status: String)
data class AdminProgramme(val id: Int, val name: String, val description: String, val minTier: Int, val active: Boolean, val lessonCount: Int)
data class ProgrammeUpsert(val name: String, val description: String?, val minTier: Int, val active: Boolean)

data class Plan(
    val id: Int,
    val name: String,
    val description: String,
    val price: Double,
    val billingFrequency: String,
    val tier: Int,
    val features: List<String>,
    /** Price for a year paid up front; null means the plan is monthly only. */
    val annualPrice: Double? = null,
    /** Reduced first-month price, only present while the launch offer runs. */
    val launchPrice: Double? = null,
    val launchEndsAt: String? = null,
    /** Free days before the first charge; 0 means no trial. */
    val trialDays: Int = 0,
)

/** A plan as the administrator sees it, including whether customers can see it (ACTIVE) or not (INACTIVE). */
data class AdminPlan(
    val id: Int,
    val name: String,
    val description: String,
    val price: Double,
    val billingFrequency: String,
    val tier: Int,
    val features: List<String>,
    val status: String,
    val annualPrice: Double? = null,
    val launchPrice: Double? = null,
    val launchEndsAt: String? = null,
    val trialDays: Int = 0,
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
    /** MONTHLY or ANNUAL. price is what one billing period costs. */
    val billing: String = "MONTHLY",
    /** True while PayFast will charge again; endDate is then the next charge date. */
    val renewing: Boolean = false,
    /** True during the free trial, before the first charge. */
    val inTrial: Boolean = false,
)

data class CheckoutRequest(val planId: Int, val billing: String? = null)

/** Shown before a plan change is confirmed. priceDifference is per month. */
data class PlanChangePreview(
    val currentPlan: String,
    val currentPrice: Double,
    val currentBilling: String,
    val newPlan: String,
    val newPrice: Double,
    val newBilling: String,
    val priceDifference: Double,
    val effectiveDate: String,
    val accessImpact: String,
)

data class Policy(val id: String, val title: String, val body: String)
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

/** A lesson as the administrator sees it, including where the video comes from. */
data class AdminLesson(
    val id: Int,
    val programmeId: Int,
    val programmeName: String,
    val title: String,
    val description: String,
    val category: String,
    val level: String,
    val durationSeconds: Int,
    val videoProvider: String,
    val videoReference: String,
    val isPreview: Boolean,
) {
    val durationLabel: String
        get() = if (durationSeconds < 90) "${durationSeconds} secs" else "${(durationSeconds + 30) / 60} mins"
}

/** A YouTube lesson has youtubeId (and an empty url); every other lesson has a short-lived signed url. */
data class Playback(val url: String, val expiresAt: String, val resumeSeconds: Int, val youtubeId: String? = null)
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
    /** CLASS, WORKSHOP, RETREAT or COACHING. */
    val kind: String = "CLASS",
    /** IN_PERSON or ONLINE. */
    val format: String = "IN_PERSON",
    /** Ticket price in rand, VAT included. 0 is a free booking. */
    val price: Double = 0.0,
    val posterUrl: String? = null,
    val salesOpenAt: String? = null,
    val salesClosesAt: String? = null,
    val memberEarlyHours: Int = 0,
    val canBuyNow: Boolean = true,
    val buyBlockedReason: String? = null,
    val onWaitlist: Boolean = false,
    val waitlistEnabled: Boolean = false,
    val joinPlatform: String? = null,
    /** Only sent to administrators, who edit them. */
    val joinLink: String? = null,
    val practicalNote: String? = null,
    val salesCloseHours: Int? = null,
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
    val reference: String = "",
    val kind: String = "CLASS",
    val format: String = "IN_PERSON",
    /** Cash plus credit paid for the ticket. 0 for a free booking. */
    val totalPaid: Double = 0.0,
    val joinPlatform: String? = null,
    val joinLink: String? = null,
    /** EVENT_CANCELLED or EVENT_RESCHEDULED: the customer has to choose what happens next. */
    val choiceNeeded: String? = null,
    /** PENDING, APPROVED or DECLINED for a cancellation asked for less than 48 hours ahead. */
    val requestStatus: String? = null,
    /** True while there are at least 48 hours left: a refund or credit for the full fee. */
    val freeCancellation: Boolean = false,
    val refundStatus: String? = null,
)

data class TicketRequest(val useCredit: Boolean = true)
/** If confirmed is false, send the customer to paymentUrl (PayFast). */
data class TicketResponse(
    val bookingId: Int, val reference: String, val confirmed: Boolean, val paymentUrl: String?,
    val amountDue: Double, val creditApplied: Double, val message: String,
)
data class TicketQuote(val price: Double, val creditAvailable: Double, val creditApplied: Double, val amountDue: Double, val canBuy: Boolean, val reason: String?)
data class CancelRequestBody(val choice: String?, val note: String?)
data class ChoiceBody(val choice: String)
data class ActionResult(val message: String, val status: String)
data class CreditItem(val id: Int, val amount: Double, val remaining: Double, val issuedAt: String, val expiresAt: String, val reason: String)
data class Credits(val balance: Double, val items: List<CreditItem>)
data class AdminEventRequest(
    val bookingId: Int, val reference: String, val fullName: String, val email: String, val eventName: String, val startTime: String,
    val totalPaid: Double, val maxAmount: Double, val choice: String?, val requestedAt: String?,
)
data class AdminRefund(
    val bookingId: Int, val reference: String, val fullName: String, val email: String, val eventName: String, val amount: Double, val payFastPaymentId: String?,
)
data class ResolveRequestBody(val approve: Boolean, val amount: Double?)
data class ReportIssueBody(val description: String)
data class VideoUploadBody(val fileName: String, val contentType: String, val sizeBytes: Long)
/** Send the file to uploadUrl with a PUT (same content type), then save key as the lesson's video. */
data class VideoUploadResult(val key: String, val uploadUrl: String, val contentType: String, val expiresAt: String)
data class GiveCreditBody(val amount: Double, val reason: String?)

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

data class AdminAttendee(val bookingId: Int, val userId: String, val fullName: String, val email: String, val bookedAt: String, val status: String)
data class AdminBooking(
    val bookingId: Int, val classId: Int, val className: String, val startTime: String, val location: String,
    val userId: String, val fullName: String, val email: String, val bookedAt: String, val status: String,
)
data class AdminSubscription(
    val id: Int, val userId: String, val fullName: String, val email: String, val planName: String, val price: Double,
    val status: String, val startDate: String?, val endDate: String?, val renewing: Boolean,
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
    val kind: String? = null,
    val format: String? = null,
    val price: Double = 0.0,
    val posterUrl: String? = null,
    val salesCloseHours: Int = 24,
    val salesOpenAt: String? = null,
    val memberEarlyHours: Int = 0,
    val joinPlatform: String? = null,
    val joinLink: String? = null,
    val practicalNote: String? = null,
    val waitlistEnabled: Boolean = false,
)

data class PlanUpsert(
    val name: String,
    val description: String?,
    val price: Double,
    val tier: Int,
    val features: String?,
    val status: String?,
    val annualPrice: Double? = null,
    val launchPrice: Double? = null,
    val launchEndsAt: String? = null,
    val trialDays: Int = 0,
)
