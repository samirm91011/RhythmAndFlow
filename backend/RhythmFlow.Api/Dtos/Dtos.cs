using System.ComponentModel.DataAnnotations;

namespace RhythmFlow.Api.Dtos;

// ---- Auth ----
public record RegisterRequest(
    [Required, StringLength(100, MinimumLength = 2)] string FullName,
    [Required, StringLength(50, MinimumLength = 3)] string Username,
    [Required, EmailAddress, StringLength(255)] string Email,
    [Required, StringLength(100, MinimumLength = 8)] string Password);

public record LoginRequest(
    [Required] string Identifier,
    [Required] string Password);

public record UserDto(Guid Id, string FullName, string Username, string Email, string Role, string About);
public record AuthResponse(string Token, UserDto User);
public record UpdateProfileRequest(
    [Required, StringLength(100, MinimumLength = 2)] string FullName,
    [Required, EmailAddress, StringLength(255)] string Email,
    [StringLength(300)] string? About);

// ---- Plans / subscriptions ----
public record PlanDto(int Id, string Name, string Description, decimal Price, string BillingFrequency, int Tier, List<string> Features);
public record SubscriptionDto(int Id, int PlanId, string PlanName, int Tier, decimal Price, string Status, DateTime? StartDate, DateTime? EndDate, bool GrantsAccess);
public record CheckoutRequest(int PlanId);
public record CheckoutResponse(int SubscriptionId, string PaymentUrl);

// ---- Content ----
public record ProgrammeDto(int Id, string Name, string Description, int MinTier, bool Locked, int LessonCount);
public record LessonDto(
    int Id, int ProgrammeId, string ProgrammeName, string Title, string Description, string Category, string Level,
    int DurationSeconds, string? ThumbnailUrl, bool IsPreview, bool Locked, decimal CompletionPercentage, int WatchTimeSeconds);
public record PlaybackDto(string Url, DateTime ExpiresAt, int ResumeSeconds);

// ---- Progress ----
public record ProgressUpdateRequest(int LessonId, [Range(0, 100000)] int WatchTimeSeconds);
public record ProgressDto(int LessonId, int WatchTimeSeconds, decimal CompletionPercentage, bool Completed);
public record ProgressSummaryDto(int CompletedLessons, int InProgressLessons, int SessionsThisMonth, int MinutesWatched);

// ---- Classes / bookings ----
public record ClassDto(
    int Id, string Name, string Description, string CoachName, string Location, DateTime StartTime, DateTime EndTime,
    int Capacity, int SpotsLeft, bool BookedByMe, string Status);
public record BookingDto(
    int Id, int ClassId, string ClassName, string CoachName, string Location, DateTime StartTime, DateTime EndTime,
    string Status, bool CanCancel);

// ---- Journal ----
public record JournalRequest(
    [Required, StringLength(20)] string Kind,
    [StringLength(40)] string? Mood,
    [StringLength(2000)] string? Text);
public record JournalDto(int Id, string Kind, string Mood, string Text, DateTime CreatedAt);

// ---- Admin ----
public record LessonUpsert(
    int ProgrammeId,
    [Required, StringLength(150)] string Title,
    [StringLength(1000)] string? Description,
    [Required] string Category,
    string? Level,
    [Range(1, 100000)] int DurationSeconds,
    [Required] string VideoProvider,
    [Required, StringLength(1000)] string VideoReference,
    bool IsPreview);

public record ClassUpsert(
    [Required, StringLength(150)] string Name,
    [StringLength(1000)] string? Description,
    [Required, StringLength(100)] string CoachName,
    [Required, StringLength(150)] string Location,
    DateTime StartTime,
    DateTime EndTime,
    [Range(1, 500)] int Capacity);

public record PlanUpsert(
    [Required, StringLength(100)] string Name,
    [StringLength(500)] string? Description,
    [Range(1, 100000)] decimal Price,
    [Range(1, 10)] int Tier,
    string? Features,
    string? Status);

public record AdminSummaryDto(int Users, int ActiveSubscriptions, int UpcomingClasses, int ActiveBookings, decimal MonthlyRecurringRevenue, int OpenErrors);

// ---- Password ----
public record ForgotPasswordRequest([Required, EmailAddress, StringLength(255)] string Email);
public record ResetPasswordRequest(
    [Required, EmailAddress, StringLength(255)] string Email,
    [Required, StringLength(10)] string Code,
    [Required, StringLength(100, MinimumLength = 8)] string NewPassword);
public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, StringLength(100, MinimumLength = 8)] string NewPassword);

// ---- Notifications ----
public record NotificationDto(int Id, string Kind, string Title, string Body, string? Route, DateTime CreatedAt, bool Read);
public record MarkReadRequest(List<int>? Ids);
public record DeviceTokenRequest([Required, StringLength(512, MinimumLength = 20)] string Token);

// ---- Error reporting ----
public record ErrorReportRequest(
    [Required, StringLength(500)] string Message,
    [StringLength(8000)] string? Details,
    [StringLength(200)] string? Route,
    [StringLength(40)] string? AppVersion,
    [StringLength(120)] string? Device,
    bool Fatal);
public record ErrorLogDto(
    int Id, string Source, string Message, string Details, string? Route, string? UserEmail, string? AppVersion,
    string? Device, int Count, DateTime FirstSeen, DateTime LastSeen, string Status);

public record DeleteAccountRequest([Required] string Password);

public record CancelResultDto(string Message, bool CancelledWithPayFast);

