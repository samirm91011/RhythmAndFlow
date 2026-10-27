using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

/// <summary>Points from the client's feedback of 20 Oct 2026 that needed finishing: priority booking, levels, retreats, grace period, technical problems.</summary>
public class ClientFeedbackTests
{
    private static (EventBookingService svc, FakeEmail email) Build(TestDb t)
    {
        var email = new FakeEmail();
        var payFast = new PayFastService(Options.Create(new PayFastOptions { MerchantId = "1", MerchantKey = "k", Passphrase = "p" }));
        return (new EventBookingService(t.Db, payFast, new NotificationService(t.Db), email, Options.Create(new EventOptions())), email);
    }

    private static ClassSession Event(TestDb t, decimal price = 250, int capacity = 10, double startsInDays = 5, string format = EventKinds.InPerson, bool waitlist = false)
    {
        var c = t.AddClass(capacity, TimeSpan.FromDays(startsInDays));
        c.Name = "Sunrise Workshop"; c.Price = price; c.Format = format; c.Kind = EventKinds.Workshop; c.WaitlistEnabled = waitlist;
        c.Location = format == EventKinds.Online ? "Online" : "Rhythm & Flow Studio";
        t.Db.SaveChanges();
        return c;
    }

    // ---- Priority booking ----

    [Fact]
    public async Task A_freed_place_is_offered_to_a_second_plan_member_before_someone_who_waited_longer()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var c = Event(t, 250, capacity: 1, waitlist: true);
        var holder = t.AddUser(); var early = t.AddUser(); var member = t.AddUser();
        t.AddSubscription(member, t.AddPlan(tier: 2, price: 249), SubscriptionStatus.Active, DateTime.UtcNow.AddDays(10));
        var (r, _) = await svc.TicketAsync(holder.Id, c.Id, false);
        await svc.ApplyPaymentResultAsync(r!.BookingId, "COMPLETE", 250m, "PF-1");

        await svc.JoinWaitlistAsync(early.Id, c.Id); await Task.Delay(5); await svc.JoinWaitlistAsync(member.Id, c.Id);
        await svc.CancelAsync(holder.Id, r.BookingId, "CREDIT", null);

        Assert.Contains(t.Db.Notifications, n => n.UserId == member.Id && n.Title == "A place opened up");
        Assert.DoesNotContain(t.Db.Notifications, n => n.UserId == early.Id && n.Title == "A place opened up");
    }

    [Fact]
    public async Task Without_a_second_plan_member_the_waiting_list_stays_first_come_first_served()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var c = Event(t, 250, capacity: 1, waitlist: true);
        var holder = t.AddUser(); var first = t.AddUser(); var second = t.AddUser();
        t.AddSubscription(second, t.AddPlan(tier: 1, price: 149), SubscriptionStatus.Active, DateTime.UtcNow.AddDays(10));   // the first plan is not priority
        var (r, _) = await svc.TicketAsync(holder.Id, c.Id, false);
        await svc.ApplyPaymentResultAsync(r!.BookingId, "COMPLETE", 250m, "PF-1");
        await svc.JoinWaitlistAsync(first.Id, c.Id); await Task.Delay(5); await svc.JoinWaitlistAsync(second.Id, c.Id);

        await svc.CancelAsync(holder.Id, r.BookingId, "CREDIT", null);

        Assert.Contains(t.Db.Notifications, n => n.UserId == first.Id && n.Title == "A place opened up");
    }

    // ---- Levels ----

    [Theory]
    [InlineData("Beginner", "Beginner")]
    [InlineData("intermediate", "Intermediate")]
    [InlineData(" Advanced ", "Advanced")]
    [InlineData("", "Beginner")]
    [InlineData(null, "Beginner")]
    public void The_three_approved_levels_are_accepted_and_a_blank_one_becomes_Beginner(string? given, string expected)
    {
        Assert.True(LessonLevels.TryNormalise(given, out var level));
        Assert.Equal(expected, level);
    }

    [Theory]
    [InlineData("All levels")]
    [InlineData("Expert")]
    public void Any_other_level_is_refused(string given) => Assert.False(LessonLevels.TryNormalise(given, out _));

    [Fact]
    public async Task Editing_a_lesson_keeps_one_of_the_three_levels_and_refuses_anything_else()
    {
        using var t = new TestDb(); var svc = new ScheduleAdminService(t.Db, new NotificationService(t.Db));
        var lesson = t.AddLesson();
        Func<string?, LessonUpsert> upsert = level => new(lesson.ProgrammeId, "Title", "d", "Yoga", level, 600, "Remote", "https://example.com/v.mp4", false);

        var bad = await svc.UpdateLessonAsync(lesson.Id, upsert("All levels"));
        Assert.False(bad.Ok); Assert.Equal(LessonLevels.Refusal, bad.Error);

        Assert.True((await svc.UpdateLessonAsync(lesson.Id, upsert("Advanced"))).Ok);
        Assert.Equal("Advanced", (await t.Db.Lessons.AsNoTracking().SingleAsync(l => l.Id == lesson.Id)).Level);
        Assert.True((await svc.UpdateLessonAsync(lesson.Id, upsert(null))).Ok);
        Assert.Equal("Beginner", (await t.Db.Lessons.AsNoTracking().SingleAsync(l => l.Id == lesson.Id)).Level);
    }

    [Fact]
    public async Task The_free_practices_from_her_channel_have_a_real_level_and_the_two_longer_workouts_are_Intermediate()
    {
        using var t = new TestDb();
        await SeedData.EnsureFreePracticesAsync(t.Db);
        var lessons = await t.Db.Lessons.AsNoTracking().ToListAsync();
        Assert.All(lessons, l => Assert.Contains(l.Level, LessonLevels.All));
        Assert.Equal(2, lessons.Count(l => l.Level == "Intermediate"));
    }

    // ---- Retreats are in person ----

    [Fact]
    public void A_retreat_cannot_be_online_but_a_workshop_or_class_can()
    {
        ClassUpsert Make(string kind, string format) =>
            new("Retreat", "d", "Deni", "Somewhere", DateTime.UtcNow.AddDays(30), DateTime.UtcNow.AddDays(32), 12, Kind: kind, Format: format, Price: 1800);

        Assert.Equal("Retreats are held in person. Choose in person.", ScheduleAdminService.ValidateEvent(Make("RETREAT", "ONLINE")));
        Assert.Null(ScheduleAdminService.ValidateEvent(Make("RETREAT", "IN_PERSON")));
        Assert.Null(ScheduleAdminService.ValidateEvent(Make("WORKSHOP", "ONLINE")));
        Assert.Null(ScheduleAdminService.ValidateEvent(Make("CLASS", "ONLINE")));
    }

    // ---- A failed or late renewal: a few days of grace ----

    [Fact]
    public async Task A_member_whose_renewal_is_late_keeps_access_for_the_grace_days_and_then_loses_it()
    {
        using var t = new TestDb(); var access = new EntitlementService(t.Db);
        var plan = t.AddPlan(tier: 2, price: 249);
        var recent = t.AddUser(); var old = t.AddUser();
        t.AddSubscription(recent, plan, SubscriptionStatus.PastDue, DateTime.UtcNow.AddDays(-(EntitlementService.GraceDays - 1)));
        t.AddSubscription(old, plan, SubscriptionStatus.PastDue, DateTime.UtcNow.AddDays(-(EntitlementService.GraceDays + 1)));

        Assert.Equal(2, await access.GetUserTierAsync(recent.Id));
        Assert.Equal(0, await access.GetUserTierAsync(old.Id));
    }

    // ---- Technical problems with an online class ----

    private static async Task<Booking> PaidBooking(TestDb t, User user, ClassSession c, decimal paid = 250)
    {
        var b = new Booking { UserId = user.Id, ClassId = c.Id, Status = BookingStatus.Booked, AmountPaid = paid };
        t.Db.Bookings.Add(b); await t.Db.SaveChangesAsync();
        return b;
    }

    [Fact]
    public async Task A_participant_can_report_a_problem_with_an_online_class_that_has_started_and_the_studio_is_told()
    {
        using var t = new TestDb(); var (svc, email) = Build(t);
        var user = t.AddUser("Thandi Mokoena"); var admin = t.AddUser("Studio", Roles.Admin);
        var c = Event(t, 250, startsInDays: -0.02, format: EventKinds.Online);
        var b = await PaidBooking(t, user, c);

        var (ok, error) = await svc.ReportIssueAsync(user.Id, b.Id, "The Zoom room would not let anyone in for the first twenty minutes.");

        Assert.True(ok, error);
        Assert.Contains(email.Sent, m => m.To == "info@rhythmandflow.co.za" && m.Body.Contains("twenty minutes") && m.Body.Contains($"RF-E{b.Id:D6}"));
        Assert.Contains(email.Sent, m => m.To == user.Email && m.Subject.StartsWith("We've received your report"));
        Assert.Contains(t.Db.Notifications, n => n.UserId == admin.Id && n.Title == "Technical problem reported");
    }

    [Fact]
    public async Task A_problem_report_needs_an_online_class_that_has_started_a_booking_that_is_yours_and_a_real_description()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var user = t.AddUser(); var other = t.AddUser();
        var upcoming = Event(t, 250, startsInDays: 2, format: EventKinds.Online);
        var inPerson = Event(t, 250, startsInDays: -0.02, format: EventKinds.InPerson);
        var tooOld = Event(t, 250, startsInDays: -5, format: EventKinds.Online);
        var online = Event(t, 250, startsInDays: -0.02, format: EventKinds.Online);
        var bUpcoming = await PaidBooking(t, user, upcoming); var bInPerson = await PaidBooking(t, user, inPerson);
        var bOld = await PaidBooking(t, user, tooOld); var bOnline = await PaidBooking(t, user, online);
        const string text = "The sound cut out for about fifteen minutes.";

        Assert.Contains("once the class has started", (await svc.ReportIssueAsync(user.Id, bUpcoming.Id, text)).Error);
        Assert.Contains("online classes", (await svc.ReportIssueAsync(user.Id, bInPerson.Id, text)).Error);
        Assert.Contains("within 48 hours", (await svc.ReportIssueAsync(user.Id, bOld.Id, text)).Error);
        Assert.Contains("couldn't find", (await svc.ReportIssueAsync(other.Id, bOnline.Id, text)).Error);
        Assert.Contains("what happened", (await svc.ReportIssueAsync(user.Id, bOnline.Id, "bad")).Error);
    }

    [Fact]
    public async Task An_administrator_can_give_credit_up_to_what_was_paid_and_the_participant_is_told()
    {
        using var t = new TestDb(); var (svc, email) = Build(t);
        var user = t.AddUser("Thandi Mokoena");
        var c = Event(t, 250, startsInDays: -0.02, format: EventKinds.Online);
        var b = await PaidBooking(t, user, c, 250);
        var free = await PaidBooking(t, t.AddUser(), c, 0);

        Assert.Contains("most for this booking", (await svc.GiveCreditAsync(b.Id, 300, null)).Error);
        Assert.Contains("at least R1", (await svc.GiveCreditAsync(b.Id, 0, null)).Error);
        Assert.Contains("free", (await svc.GiveCreditAsync(free.Id, 50, null)).Error);

        var (ok, error) = await svc.GiveCreditAsync(b.Id, 100, "Zoom problem");

        Assert.True(ok, error);
        var credit = await t.Db.Credits.AsNoTracking().SingleAsync(x => x.UserId == user.Id);
        Assert.Equal(100m, credit.Remaining); Assert.Equal("Zoom problem", credit.Reason);
        Assert.True(credit.ExpiresAt > DateTime.UtcNow.AddMonths(11));
        Assert.Contains(email.Sent, m => m.To == user.Email && m.Subject.Contains("R100.00"));
        Assert.Equal(100m, await svc.CreditBalanceAsync(user.Id, DateTime.UtcNow));
    }
}
