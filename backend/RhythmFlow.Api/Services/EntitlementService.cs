using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;

namespace RhythmFlow.Api.Services;

/// <summary>Decides whether a user may watch restricted content (BR-04, BR-06, BR-07).</summary>
public class EntitlementService(AppDbContext db)
{
    /// <summary>
    /// Days a member keeps access after a renewal payment fails or is late, while PayFast tries again. The client's renewal policy says a
    /// grace period will be communicated clearly but did not give one, so this is 3 days. Change it with Billing:GraceDays.
    /// </summary>
    public static int GraceDays { get; set; } = 3;

    /// <summary>Highest tier the user currently has access to (0 = none).</summary>
    public async Task<int> GetUserTierAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        var graceCutoff = now.AddDays(-GraceDays);
        var tiers = await db.Subscriptions
            .Where(s => s.UserId == userId &&
                ((s.Status == SubscriptionStatus.Active && (s.StartDate == null || s.StartDate <= now) && (s.EndDate == null || s.EndDate > now)) ||
                 (s.Status == SubscriptionStatus.Cancelled && s.EndDate != null && s.EndDate > now) ||
                 (s.Status == SubscriptionStatus.PastDue && s.EndDate != null && s.EndDate > graceCutoff)))
            .Select(s => s.Plan!.Tier)
            .ToListAsync();
        return tiers.Count == 0 ? 0 : tiers.Max();
    }

    // Active subscriptions, or cancelled ones that are still inside the period already paid for.
    private static bool GrantsAccessExpr(Subscription s, DateTime now) =>
        (s.Status == SubscriptionStatus.Active && (s.StartDate == null || s.StartDate <= now) && (s.EndDate == null || s.EndDate > now)) ||
        (s.Status == SubscriptionStatus.Cancelled && s.EndDate != null && s.EndDate > now) ||
        (s.Status == SubscriptionStatus.PastDue && s.EndDate != null && s.EndDate > now.AddDays(-GraceDays));

    public static bool GrantsAccess(Subscription s) => GrantsAccessExpr(s, DateTime.UtcNow);

    public static bool CanWatch(Lesson lesson, int userTier, bool isAdmin) =>
        isAdmin || lesson.IsPreview || userTier >= (lesson.Programme?.MinTier ?? 1);
}
