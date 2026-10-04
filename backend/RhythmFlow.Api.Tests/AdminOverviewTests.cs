using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

public class AdminOverviewTests
{
    private static Booking Book(TestDb t, User u, ClassSession c, string status = BookingStatus.Booked, int minutesAgo = 0)
    {
        var b = new Booking { UserId = u.Id, ClassId = c.Id, Status = status, BookingDate = DateTime.UtcNow.AddMinutes(-minutesAgo) };
        t.Db.Bookings.Add(b);
        t.Db.SaveChanges();
        return b;
    }

    [Fact]
    public async Task Attendees_lists_only_people_still_booked_earliest_first_with_name_and_email()
    {
        using var t = new TestDb();
        var svc = new AdminOverviewService(t.Db);
        var cls = t.AddClass();
        var late = t.AddUser("Late Booker"); var early = t.AddUser("Early Booker"); var left = t.AddUser("Cancelled Person");
        Book(t, late, cls, minutesAgo: 5); Book(t, early, cls, minutesAgo: 50); Book(t, left, cls, BookingStatus.Cancelled);

        var list = await svc.AttendeesAsync(cls.Id);

        Assert.NotNull(list);
        Assert.Equal(["Early Booker", "Late Booker"], list!.Select(a => a.FullName).ToArray());
        Assert.Equal(early.Email, list[0].Email);
    }

    [Fact]
    public async Task Attendees_of_an_unknown_class_is_null_and_of_an_empty_class_is_empty()
    {
        using var t = new TestDb();
        var svc = new AdminOverviewService(t.Db);
        var cls = t.AddClass();

        Assert.Null(await svc.AttendeesAsync(9999));
        Assert.Empty((await svc.AttendeesAsync(cls.Id))!);
    }

    [Fact]
    public async Task Upcoming_bookings_skip_past_cancelled_and_called_off_classes_and_sort_by_class_time()
    {
        using var t = new TestDb();
        var svc = new AdminOverviewService(t.Db);
        var soon = t.AddClass(startsIn: TimeSpan.FromDays(1)); var later = t.AddClass(startsIn: TimeSpan.FromDays(3));
        var past = t.AddClass(startsIn: TimeSpan.FromDays(-1));
        var calledOff = t.AddClass(startsIn: TimeSpan.FromDays(2)); calledOff.Status = "CANCELLED"; t.Db.SaveChanges();
        var u = t.AddUser("Member");
        Book(t, u, later); Book(t, u, soon); Book(t, u, past); Book(t, u, calledOff); Book(t, u, t.AddClass(), BookingStatus.Cancelled);

        var list = await svc.UpcomingBookingsAsync();

        Assert.Equal([soon.Id, later.Id], list.Select(b => b.ClassId).ToArray());
        Assert.All(list, b => Assert.Equal("Member", b.FullName));
    }

    [Fact]
    public async Task Upcoming_bookings_count_matches_the_admin_summary_rule()
    {
        using var t = new TestDb();
        var svc = new AdminOverviewService(t.Db);
        var cls = t.AddClass();
        Book(t, t.AddUser("A"), cls); Book(t, t.AddUser("B"), cls);

        Assert.Equal(2, (await svc.UpcomingBookingsAsync()).Count);
    }

    [Fact]
    public async Task Active_subscriptions_lists_who_holds_which_plan_and_leaves_out_ended_pending_and_failed()
    {
        using var t = new TestDb();
        var svc = new AdminOverviewService(t.Db);
        var flow = t.AddPlan(1, 99); var rhythm = t.AddPlan(2, 199);
        var a = t.AddUser("Alex"); var b = t.AddUser("Bea"); var c = t.AddUser("Cleo"); var d = t.AddUser("Dan"); var e = t.AddUser("Eve");
        t.AddSubscription(a, flow, SubscriptionStatus.Active, DateTime.UtcNow.AddDays(20));
        t.AddSubscription(b, rhythm, SubscriptionStatus.Active, null);
        t.AddSubscription(c, flow, SubscriptionStatus.Active, DateTime.UtcNow.AddDays(-1));   // period over
        t.AddSubscription(d, flow, SubscriptionStatus.Pending);
        t.AddSubscription(e, flow, SubscriptionStatus.Cancelled, DateTime.UtcNow.AddDays(5));

        var list = await svc.ActiveSubscriptionsAsync();

        Assert.Equal(["Alex", "Bea"], list.Select(s => s.FullName).OrderBy(n => n).ToArray());
        Assert.Equal(199m, list.Single(s => s.FullName == "Bea").Price);
        Assert.Equal("Plan 1", list.Single(s => s.FullName == "Alex").PlanName);
    }
}
