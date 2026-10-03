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
    public string Level { get; set; } = "All levels";
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
}

public class Booking
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public int ClassId { get; set; }
    public ClassSession? Class { get; set; }
    public DateTime BookingDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = BookingStatus.Booked;
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