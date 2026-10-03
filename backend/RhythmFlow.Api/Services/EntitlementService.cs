using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;

namespace RhythmFlow.Api.Services;

/// <summary>Decides whether a user may watch restricted content (BR-04, BR-06, BR-07).</summary>
public class EntitlementService(AppDbContext db)
{
    /// <summary>Highest tier the user currently has access to (0 = none).</summary>
    public async Task<int> GetUserTierAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        var tiers = await db.Subscriptions
            .Where(s => s.UserId == userId &&
                ((s.Status == SubscriptionStatus.Active && (s.EndDate == null || s.EndDate > now)) ||
                 (s.Status == SubscriptionStatus.Cancelled && s.EndDate != null && s.EndDate > now)))
            .Select(s => s.Plan!.Tier)
            .ToListAsync();
        return tiers.Count == 0 ? 0 : tiers.Max();
    }

    // Active subscriptions, or cancelled ones that are still inside the period already paid for.
    private static bool GrantsAccessExpr(Subscription s, DateTime now) =>
        (s.Status == SubscriptionStatus.Active && (s.EndDate == null || s.EndDate > now)) ||
        (s.Status == SubscriptionStatus.Cancelled && s.EndDate != null && s.EndDate > now);

    public static bool GrantsAccess(Subscription s) => GrantsAccessExpr(s, DateTime.UtcNow);

    public static bool CanWatch(Lesson lesson, int userTier, bool isAdmin) =>
        isAdmin || lesson.IsPreview || userTier >= (lesson.Programme?.MinTier ?? 1);
}
