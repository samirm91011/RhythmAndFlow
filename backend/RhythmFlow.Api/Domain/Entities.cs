namespace RhythmFlow.Api.Domain;

public static class Roles
{
    public const string Customer = "CUSTOMER";
    public const string Admin = "ADMIN";
}

public static class SubscriptionStatus
{
    public const string Pending = "PENDING";
    public const string Active = "ACTIVE";
    public const string PastDue = "PAST_DUE";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";
    public const string Failed = "FAILED";
}

public static class BookingStatus
{
    public const string Booked = "BOOKED";
    public const string Cancelled = "CANCELLED";
    public const string Completed = "COMPLETED";
    /// <summary>A ticket is held for a short time while the customer pays on PayFast.</summary>
    public const string PendingPayment = "PENDING_PAYMENT";
    /// <summary>On the waiting list for a sold-out event. A spot that opens is held for the first person for a while (HoldUntil).</summary>
    public const string Waitlisted = "WAITLISTED";
    /// <summary>The studio cancelled the event; the customer is choosing a refund or account credit.</summary>
    public const string EventCancelled = "EVENT_CANCELLED";
}

public static class EventKinds
{
    public const string Class = "CLASS";
    public const string Workshop = "WORKSHOP";
    public const string Retreat = "RETREAT";
    public const string Coaching = "COACHING";
    public static readonly string[] All = [Class, Workshop, Retreat, Coaching];
    public const string Online = "ONLINE";
    public const string InPerson = "IN_PERSON";
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = "";
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Customer;
    public string AccountStatus { get; set; } = "ACTIVE";
    public string About { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Changes when the password changes or the account is disabled, which invalidates old login tokens.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>False until the person types the code we emailed them. New sign-ups start false; accounts that existed before this was added, and seeded accounts, count as verified.</summary>
    public bool EmailVerified { get; set; } = true;
}

public class SubscriptionPlan
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public string BillingFrequency { get; set; } = "MONTHLY";
    public string Status { get; set; } = "ACTIVE";
    /// <summary>Access level. A plan unlocks every programme whose MinTier is less than or equal to this.</summary>
    public int Tier { get; set; } = 1;
    public string Features { get; set; } = "";
    /// <summary>Price for a whole year paid up front. Null means this plan is monthly only.</summary>
    public decimal? AnnualPrice { get; set; }
    /// <summary>Reduced price for the first month only, while the launch offer runs. Null means no launch offer.</summary>
    public decimal? LaunchPrice { get; set; }
    /// <summary>Last moment (UTC) a new subscriber can still get the launch price.</summary>
    public DateTime? LaunchEndsAt { get; set; }
    /// <summary>Free days before the first charge. 0 means no free trial.</summary>
    public int TrialDays { get; set; }
}

public class Subscription
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public int PlanId { get; set; }
    public SubscriptionPlan? Plan { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = SubscriptionStatus.Pending;
    public string? PayFastReference { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>MONTHLY or ANNUAL.</summary>
    public string Billing { get; set; } = "MONTHLY";
    /// <summary>What the very first PayFast payment is expected to be (launch price, full price, or 0 for a trial or a plan change). Null on subscriptions made before this existed.</summary>
    public decimal? InitialAmount { get; set; }
    /// <summary>Free trial length chosen at checkout (0 = none).</summary>
    public int TrialDays { get; set; }
    /// <summary>For a plan change: the day the new plan starts (the end of the plan it replaces).</summary>
    public DateTime? PlannedStart { get; set; }
    /// <summary>True after a free-start checkout until PayFast takes the first real payment.</summary>
    public bool AwaitingFirstCharge { get; set; }
    /// <summary>The end date a renewal reminder was already sent for, so each reminder goes out once.</summary>
    public DateTime? ReminderForEnd { get; set; }
    /// <summary>What each period costs this person, fixed when they subscribed. A later price change on the plan does not touch it. Null on older subscriptions, which fall back to the plan price.</summary>
    public decimal? LockedPrice { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "";
    public string? TransactionReference { get; set; }
}

public class FitnessProgramme
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "ACTIVE";
    public int MinTier { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Lesson> Lessons { get; set; } = new();
}

public class Lesson
{
    public int Id { get; set; }
    public int ProgrammeId { get; set; }
    public FitnessProgramme? Programme { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "Yoga";
    public string Level { get; set; } = "Beginner";   // Beginner, Intermediate or Advanced (see LessonLevels)
    public int DurationSeconds { get; set; }
    public int SequenceNumber { get; set; }
    /// <summary>Where the video lives: "Remote" (proxied from VideoReference URL) or "Local" (file in media folder).</summary>
    public string VideoProvider { get; set; } = "Remote";
    public string VideoReference { get; set; } = "";
    public string? ThumbnailUrl { get; set; }
    /// <summary>Free preview lessons are playable without a subscription.</summary>
    public bool IsPreview { get; set; }
}

public class Progress
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public int LessonId { get; set; }
    public Lesson? Lesson { get; set; }
    public int WatchTimeSeconds { get; set; }
    public decimal CompletionPercentage { get; set; }
    public DateTime LastWatchedAt { get; set; } = DateTime.UtcNow;
    public bool Completed { get; set; }
}

public class ClassSession
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string CoachName { get; set; } = "";
    public string Location { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Capacity { get; set; } = 20;
    public string Status { get; set; } = "SCHEDULED";
    /// <summary>CLASS, WORKSHOP, RETREAT or COACHING. Decides the practical note in the booking confirmation.</summary>
    public string Kind { get; set; } = EventKinds.Class;
    /// <summary>IN_PERSON (Location is the venue) or ONLINE.</summary>
    public string Format { get; set; } = EventKinds.InPerson;
    /// <summary>Ticket price in rand, VAT included. 0 means a free booking, as before.</summary>
    public decimal Price { get; set; }
    public string? PosterUrl { get; set; }
    /// <summary>Ticket sales close this many hours before the start.</summary>
    public int SalesCloseHours { get; set; } = 24;
    /// <summary>When ticket sales open to everyone. Null means they are already open.</summary>
    public DateTime? SalesOpenAt { get; set; }
    /// <summary>Members of the second plan can buy this many hours before SalesOpenAt.</summary>
    public int MemberEarlyHours { get; set; }
    /// <summary>Zoom or Google Meet, for an online event.</summary>
    public string? JoinPlatform { get; set; }
    public string? JoinLink { get; set; }
    /// <summary>Replaces the standard "what to bring" paragraph in the confirmation when set.</summary>
    public string? PracticalNote { get; set; }
    public bool WaitlistEnabled { get; set; }
}

public class Booking
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public int ClassId { get; set; }
    public ClassSession? Class { get; set; }
    public DateTime BookingDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = BookingStatus.Booked;
    /// <summary>The part of the ticket price that goes through PayFast (price minus any account credit).</summary>
    public decimal AmountDue { get; set; }
    /// <summary>The part of the price paid with account credit.</summary>
    public decimal CreditApplied { get; set; }
    /// <summary>Which credits were used, as "id:amount;id:amount", so they can be given back if the payment is abandoned.</summary>
    public string? CreditUses { get; set; }
    /// <summary>Cash actually received through PayFast.</summary>
    public decimal AmountPaid { get; set; }
    public string? PayFastPaymentId { get; set; }
    /// <summary>A pending ticket, or an offered waiting-list spot, is held until this moment.</summary>
    public DateTime? HoldUntil { get; set; }
    /// <summary>EVENT_CANCELLED or EVENT_RESCHEDULED: the customer has to choose what happens next.</summary>
    public string? ChoiceNeeded { get; set; }
    /// <summary>REQUESTED (waiting for an administrator to refund it on PayFast) or REFUNDED.</summary>
    public string? RefundStatus { get; set; }
    public decimal RefundAmount { get; set; }
    /// <summary>A cancellation asked for less than 48 hours before the event: PENDING, APPROVED or DECLINED.</summary>
    public string? RequestStatus { get; set; }
    /// <summary>REFUND or CREDIT: what the customer asked for.</summary>
    public string? RequestChoice { get; set; }
    public DateTime? RequestedAt { get; set; }
    public DateTime? LinkSentAt { get; set; }
    public bool DayReminderSent { get; set; }
    public bool HourReminderSent { get; set; }
}

/// <summary>Money the studio owes a customer as credit towards a future event. Valid for 12 months from the day it is issued.</summary>
public class AccountCredit
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public decimal Remaining { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public string Reason { get; set; } = "";
    public int? BookingId { get; set; }
}

public class JournalEntry
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    /// <summary>CHECKIN, REFLECTION or GRATITUDE.</summary>
    public string Kind { get; set; } = "REFLECTION";
    public string Mood { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PasswordResetCode
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public string CodeHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>RESET (forgot password) or VERIFY (confirming an email address). A code for one can never be used for the other.</summary>
    public string Purpose { get; set; } = "RESET";
}

/// <summary>An in-app notification. The app also shows these as system notifications.</summary>
public class AppNotification
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    /// <summary>BOOKING, CLASS, SUBSCRIPTION, PAYMENT, ADMIN_ERROR, INFO</summary>
    public string Kind { get; set; } = "INFO";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    /// <summary>App screen to open when tapped (e.g. "bookings", "subscription", "admin/errors").</summary>
    public string? Route { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}

/// <summary>A phone that can receive push notifications for a user (a Firebase registration token).</summary>
public class DeviceToken
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = "";
    public string Platform { get; set; } = "ANDROID";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}

/// <summary>A de-duplicated error report from the API or the app, for administrators to review.</summary>
/// <summary>The wording the client edits from the admin screens (moods, affirmations, quotes, journal prompts), stored as one JSON document.</summary>
public class AppContent
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Json { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ErrorLog
{
    public int Id { get; set; }
    /// <summary>API or APP</summary>
    public string Source { get; set; } = "API";
    public string Fingerprint { get; set; } = "";
    public string Message { get; set; } = "";
    public string Details { get; set; } = "";
    public string? Route { get; set; }
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? AppVersion { get; set; }
    public string? Device { get; set; }
    public int Count { get; set; } = 1;
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public DateTime? LastNotified { get; set; }
    /// <summary>NEW or RESOLVED</summary>
    public string Status { get; set; } = "NEW";
}