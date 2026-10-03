using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

public class ProgressTests
{
    [Fact]
    public async Task Completion_is_capped_at_100_percent()
    {
        using var t = new TestDb();
        var user = t.AddUser(); var lesson = t.AddLesson(seconds: 100);
        var result = await new ProgressService(t.Db).UpdateAsync(user.Id, lesson, 500);
        Assert.Equal(100m, result.CompletionPercentage);
        Assert.Equal(100, result.WatchTimeSeconds);
        Assert.True(result.Completed);
    }

    [Fact]
    public async Task Watch_time_never_goes_backwards()
    {
        using var t = new TestDb();
        var user = t.AddUser(); var lesson = t.AddLesson(seconds: 100);
        var svc = new ProgressService(t.Db);
        await svc.UpdateAsync(user.Id, lesson, 60);
        var result = await svc.UpdateAsync(user.Id, lesson, 20);
        Assert.Equal(60, result.WatchTimeSeconds);
        Assert.Equal(60m, result.CompletionPercentage);
        Assert.False(result.Completed);
    }

    [Fact]
    public async Task Lesson_counts_as_completed_from_95_percent()
    {
        using var t = new TestDb();
        var user = t.AddUser(); var lesson = t.AddLesson(seconds: 200);
        var svc = new ProgressService(t.Db);
        Assert.False((await svc.UpdateAsync(user.Id, lesson, 189)).Completed); // 94.5%
        Assert.True((await svc.UpdateAsync(user.Id, lesson, 190)).Completed);  // 95%
    }

    [Fact]
    public async Task Summary_counts_completed_and_in_progress_lessons()
    {
        using var t = new TestDb();
        var user = t.AddUser(); var done = t.AddLesson(seconds: 10); var half = t.AddLesson(seconds: 100);
        var svc = new ProgressService(t.Db);
        await svc.UpdateAsync(user.Id, done, 10);
        await svc.UpdateAsync(user.Id, half, 50);
        var s = await svc.SummaryAsync(user.Id);
        Assert.Equal(1, s.CompletedLessons);
        Assert.Equal(1, s.InProgressLessons);
        Assert.Equal(2, s.SessionsThisMonth);
    }
}

public class EntitlementTests
{
    [Fact]
    public async Task No_subscription_means_no_access_tier()
    {
        using var t = new TestDb();
        Assert.Equal(0, await new EntitlementService(t.Db).GetUserTierAsync(t.AddUser().Id));
    }

    [Fact]
    public async Task Active_subscription_gives_the_plan_tier()
    {
        using var t = new TestDb();
        var u = t.AddUser(); t.AddSubscription(u, t.AddPlan(tier: 2), SubscriptionStatus.Active, DateTime.UtcNow.AddDays(10));
        Assert.Equal(2, await new EntitlementService(t.Db).GetUserTierAsync(u.Id));
    }

    [Fact]
    public async Task Pending_subscription_gives_no_access_until_payment_is_confirmed()
    {
        using var t = new TestDb();
        var u = t.AddUser(); t.AddSubscription(u, t.AddPlan(tier: 3), SubscriptionStatus.Pending);
        Assert.Equal(0, await new EntitlementService(t.Db).GetUserTierAsync(u.Id));
    }

    [Fact]
    public async Task Cancelled_subscription_keeps_access_until_the_paid_period_ends()
    {
        using var t = new TestDb();
        var plan = t.AddPlan(tier: 2);
        var paid = t.AddUser(); t.AddSubscription(paid, plan, SubscriptionStatus.Cancelled, DateTime.UtcNow.AddDays(3));
        var over = t.AddUser(); t.AddSubscription(over, plan, SubscriptionStatus.Cancelled, DateTime.UtcNow.AddDays(-1));
        var svc = new EntitlementService(t.Db);
        Assert.Equal(2, await svc.GetUserTierAsync(paid.Id));
        Assert.Equal(0, await svc.GetUserTierAsync(over.Id));
    }

    [Fact]
    public async Task Active_subscription_past_its_end_date_loses_access()
    {
        using var t = new TestDb();
        var u = t.AddUser(); t.AddSubscription(u, t.AddPlan(), SubscriptionStatus.Active, DateTime.UtcNow.AddMinutes(-5));
        Assert.Equal(0, await new EntitlementService(t.Db).GetUserTierAsync(u.Id));
    }

    [Fact]
    public async Task Highest_tier_wins_when_a_user_has_several_subscriptions()
    {
        using var t = new TestDb();
        var u = t.AddUser();
        t.AddSubscription(u, t.AddPlan(tier: 1), SubscriptionStatus.Active, DateTime.UtcNow.AddDays(5));
        t.AddSubscription(u, t.AddPlan(tier: 3), SubscriptionStatus.Active, DateTime.UtcNow.AddDays(5));
        Assert.Equal(3, await new EntitlementService(t.Db).GetUserTierAsync(u.Id));
    }

    [Fact]
    public void CanWatch_follows_the_content_rules()
    {
        using var t = new TestDb();
        var locked = t.AddLesson(minTier: 2);
        var preview = t.AddLesson(minTier: 3, preview: true);
        Assert.False(EntitlementService.CanWatch(locked, userTier: 1, isAdmin: false));
        Assert.True(EntitlementService.CanWatch(locked, userTier: 2, isAdmin: false));
        Assert.True(EntitlementService.CanWatch(preview, userTier: 0, isAdmin: false)); // free preview
        Assert.True(EntitlementService.CanWatch(locked, userTier: 0, isAdmin: true));   // admins see everything
    }
}
