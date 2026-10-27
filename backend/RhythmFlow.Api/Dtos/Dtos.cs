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
/// <summary>Sign-up no longer signs the person in. They confirm their email with a code first, then sign in.</summary>
public record RegisterResponse(string Email, bool VerificationRequired, string Message);
public record VerifyEmailRequest(
    [Required, EmailAddress, StringLength(255)] string Email,
    [Required, StringLength(10)] string Code);
public record ResendVerificationRequest([Required, EmailAddress, StringLength(255)] string Email);
public record UpdateProfileRequest(
    [Required, StringLength(100, MinimumLength = 2)] string FullName,
    [Required, EmailAddress, StringLength(255)] string Email,
    [StringLength(300)] string? About);

// ---- Plans / subscriptions ----
/// <summary>A plan as customers see it. LaunchPrice is only filled in while the launch offer is running.</summary>
public record PlanDto(
    int Id, string Name, string Description, decimal Price, string BillingFrequency, int Tier, List<string> Features,
    decimal? AnnualPrice = null, decimal? LaunchPrice = null, DateTime? LaunchEndsAt = null, int TrialDays = 0);
/// <summary>Price is what one billing period costs. Renewing is true while PayFast will charge again; the next charge or expiry date is EndDate.</summary>
public record SubscriptionDto(
    int Id, int PlanId, string PlanName, int Tier, decimal Price, string Status, DateTime? StartDate, DateTime? EndDate, bool GrantsAccess,
    string Billing = "MONTHLY", bool Renewing = false, bool InTrial = false);
public record CheckoutRequest(int PlanId, string? Billing = null);
/// <summary>Shown before a plan change is confirmed: what it costs, when it starts and what happens to access.</summary>
public record PlanChangePreviewDto(
    string CurrentPlan, decimal CurrentPrice, string CurrentBilling, string NewPlan, decimal NewPrice, string NewBilling,
    decimal PriceDifference, DateTime EffectiveDate, string AccessImpact);
public record PolicyDto(string Id, string Title, string Body);
public record CheckoutResponse(int SubscriptionId, string PaymentUrl);

// ---- Content ----
public record ProgrammeDto(int Id, string Name, string Description, int MinTier, bool Locked, int LessonCount);
public record LessonDto(
    int Id, int ProgrammeId, string ProgrammeName, string Title, string Description, string Category, string Level,
    int DurationSeconds, string? ThumbnailUrl, bool IsPreview, bool Locked, decimal CompletionPercentage, int WatchTimeSeconds);
/// <summary>How to play a lesson. A YouTube lesson has YouTubeId and no Url; every other lesson has a short-lived signed Url.\n/// The JSON name is "youtubeId" (all lower case after "you") because that is what the Android app and the web app read.</summary>
public record PlaybackDto(string Url, DateTime ExpiresAt, int ResumeSeconds, [property: System.Text.Json.Serialization.JsonPropertyName("youtubeId")] string? YouTubeId = null);

// ---- Progress ----
public record ProgressUpdateRequest(int LessonId, [Range(0, 100000)] int WatchTimeSeconds);
public record ProgressDto(int LessonId, int WatchTimeSeconds, decimal CompletionPercentage, bool Completed);
public record ProgressSummaryDto(int CompletedLessons, int InProgressLessons, int SessionsThisMonth, int MinutesWatched);

// ---- Classes / bookings ----
/// <summary>
/// A class, workshop, retreat or coaching session. Price 0 is a free booking. For a paid event CanBuyNow says whether this person can
/// buy a ticket right now and BuyBlockedReason says why not (sales not open yet, closed, sold out).
/// </summary>
public record ClassDto(
    int Id, string Name, string Description, string CoachName, string Location, DateTime StartTime, DateTime EndTime,
    int Capacity, int SpotsLeft, bool BookedByMe, string Status,
    string Kind = "CLASS", string Format = "IN_PERSON", decimal Price = 0, string? PosterUrl = null,
    DateTime? SalesOpenAt = null, DateTime? SalesClosesAt = null, int MemberEarlyHours = 0,
    bool CanBuyNow = true, string? BuyBlockedReason = null, bool OnWaitlist = false, bool WaitlistEnabled = false,
    string? JoinPlatform = null,
    // Only filled in for administrators, who edit these.
    string? JoinLink = null, string? PracticalNote = null, int? SalesCloseHours = null);
/// <summary>
/// One of my bookings. TotalPaid is cash plus credit. FreeCancellation is true while there are at least 48 hours left.
/// ChoiceNeeded is set when the studio cancelled or moved the event and the customer must choose what happens next.
/// JoinLink is only given for a confirmed online event.
/// </summary>
public record BookingDto(
    int Id, int ClassId, string ClassName, string CoachName, string Location, DateTime StartTime, DateTime EndTime,
    string Status, bool CanCancel,
    string Reference = "", string Kind = "CLASS", string Format = "IN_PERSON", decimal TotalPaid = 0,
    string? JoinPlatform = null, string? JoinLink = null, string? ChoiceNeeded = null, string? RequestStatus = null,
    bool FreeCancellation = false, string? RefundStatus = null);

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
    [Range(1, 500)] int Capacity,
    string? Kind = null,
    string? Format = null,
    [Range(0, 100000)] decimal Price = 0,
    [StringLength(1000)] string? PosterUrl = null,
    [Range(0, 2000)] int SalesCloseHours = 24,
    DateTime? SalesOpenAt = null,
    [Range(0, 2000)] int MemberEarlyHours = 0,
    [StringLength(30)] string? JoinPlatform = null,
    [StringLength(1000)] string? JoinLink = null,
    [StringLength(2000)] string? PracticalNote = null,
    bool WaitlistEnabled = false);

public record PlanUpsert(
    [Required, StringLength(100)] string Name,
    [StringLength(500)] string? Description,
    [Range(1, 100000)] decimal Price,
    [Range(1, 10)] int Tier,
    string? Features,
    string? Status,
    decimal? AnnualPrice = null,
    decimal? LaunchPrice = null,
    DateTime? LaunchEndsAt = null,
    [Range(0, 60)] int TrialDays = 0);

/// <summary>A lesson as the administrator sees it, including where the video comes from (customers never see this).</summary>
public record AdminLessonDto(
    int Id, int ProgrammeId, string ProgrammeName, string Title, string Description, string Category, string Level,
    int DurationSeconds, string VideoProvider, string VideoReference, bool IsPreview);

public record AdminPlanDto(
    int Id, string Name, string Description, decimal Price, string BillingFrequency, int Tier, List<string> Features, string Status,
    decimal? AnnualPrice = null, decimal? LaunchPrice = null, DateTime? LaunchEndsAt = null, int TrialDays = 0);

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

// ---- Programme administration (FR-22) ----
public record ProgrammeUpsert(
    [Required, StringLength(100)] string Name,
    [StringLength(500)] string? Description,
    [Range(1, 10)] int MinTier,
    bool Active = true);
public record AdminProgrammeDto(int Id, string Name, string Description, int MinTier, bool Active, int LessonCount);

// ---- Payments and user administration ----
public record PaymentDto(int Id, string PlanName, decimal Amount, string Status, DateTime Date, string Receipt);
public record AdminUserDto(Guid Id, string FullName, string Username, string Email, string Role, string Status, DateTime CreatedAt, string? Plan);
public record SetUserStatusRequest([Required, RegularExpression("^(ACTIVE|DISABLED)$")] string Status);

// ---- Admin overview lists: who is booked, who holds a plan ----
public record AdminAttendeeDto(int BookingId, Guid UserId, string FullName, string Email, DateTime BookedAt, string Status);
public record AdminBookingDto(
    int BookingId, int ClassId, string ClassName, DateTime StartTime, string Location,
    Guid UserId, string FullName, string Email, DateTime BookedAt, string Status);
public record AdminSubscriptionDto(
    int Id, Guid UserId, string FullName, string Email, string PlanName, decimal Price, string Status,
    DateTime? StartDate, DateTime? EndDate, bool Renewing);

public record CancelResultDto(string Message, bool CancelledWithPayFast);


// ---- Event tickets, credits and refunds ----
public record TicketRequest(bool UseCredit = true);
/// <summary>Result of asking for a ticket. If Confirmed is false, send the customer to PaymentUrl (PayFast).</summary>
public record TicketResponse(int BookingId, string Reference, bool Confirmed, string? PaymentUrl, decimal AmountDue, decimal CreditApplied, string Message);
public record TicketQuoteDto(decimal Price, decimal CreditAvailable, decimal CreditApplied, decimal AmountDue, bool CanBuy, string? Reason);
public record CancelRequestDto(string? Choice, [StringLength(500)] string? Note);
public record ChoiceRequest([Required] string Choice);
public record ActionResultDto(string Message, string Status);
public record CreditDto(int Id, decimal Amount, decimal Remaining, DateTime IssuedAt, DateTime ExpiresAt, string Reason);
public record CreditsDto(decimal Balance, List<CreditDto> Items);
public record AdminEventRequestDto(
    int BookingId, string Reference, string FullName, string Email, string EventName, DateTime StartTime,
    decimal TotalPaid, decimal MaxAmount, string? Choice, DateTime? RequestedAt);
public record AdminRefundDto(
    int BookingId, string Reference, string FullName, string Email, string EventName, decimal Amount, string? PayFastPaymentId);
public record ResolveRequestRequest(bool Approve, decimal? Amount);
public record ReportIssueRequest([Required, StringLength(1000)] string Description);
public record GiveCreditRequest(decimal Amount, [StringLength(200)] string? Reason);