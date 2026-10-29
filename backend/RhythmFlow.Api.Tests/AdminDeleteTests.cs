using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

/// <summary>The delete buttons in the admin screens: classes, customers, programmes, plans and error reports.</summary>
public class AdminDeleteTests
{
    private static ScheduleAdminService Schedule(TestDb t) => new(t.Db, new NotificationService(t.Db));

    private static Booking Book(TestDb t, ClassSession c, User u, decimal paid = 0, string status = BookingStatus.Booked)
    {
        var b = new Booking { UserId = u.Id, ClassId = c.Id, Status = status, AmountPaid = paid };
        t.Db.Bookings.Add(b); t.Db.SaveChanges();
        return b;
    }

    [Fact]
    public async Task A_cancelled_class_with_only_free_bookings_is_deleted_with_them()
    {
        using var t = new TestDb(); var u = t.AddUser(); var c = t.AddClass(); c.Status = "CANCELLED"; t.Db.SaveChanges(); Book(t, c, u);

        var (ok, _, error) = await Schedule(t).DeleteClassAsync(c.Id);

        Assert.True(ok, error); Assert.Empty(t.Db.Classes); Assert.Empty(t.Db.Bookings);
    }

    [Fact]
    public async Task A_class_that_already_took_place_can_be_deleted()
    {
        using var t = new TestDb(); var u = t.AddUser(); var c = t.AddClass(startsIn: TimeSpan.FromDays(-3)); Book(t, c, u);
        Assert.True((await Schedule(t).DeleteClassAsync(c.Id)).Ok);
    }

    [Fact]
    public async Task An_upcoming_class_with_people_booked_must_be_cancelled_first_so_they_are_told()
    {
        using var t = new TestDb(); var u = t.AddUser(); var c = t.AddClass(); Book(t, c, u);

        var (ok, _, error) = await Schedule(t).DeleteClassAsync(c.Id);

        Assert.False(ok); Assert.Contains("Cancel the class first", error); Assert.Single(t.Db.Classes);
    }

    [Fact]
    public async Task An_upcoming_class_nobody_booked_can_simply_be_deleted()
    {
        using var t = new TestDb(); var c = t.AddClass();
        Assert.True((await Schedule(t).DeleteClassAsync(c.Id)).Ok); Assert.Empty(t.Db.Classes);
    }

    [Fact]
    public async Task A_class_people_paid_for_is_kept_as_a_record_even_when_cancelled()
    {
        using var t = new TestDb(); var u = t.AddUser(); var c = t.AddClass(); c.Status = "CANCELLED"; t.Db.SaveChanges(); Book(t, c, u, paid: 250);

        var (ok, _, error) = await Schedule(t).DeleteClassAsync(c.Id);

        Assert.False(ok); Assert.Contains("stay on record", error); Assert.Single(t.Db.Classes); Assert.Single(t.Db.Bookings);
    }

    [Fact]
    public async Task A_booking_owed_a_refund_or_an_answer_also_keeps_the_class()
    {
        using var t = new TestDb(); var u = t.AddUser(); var c = t.AddClass(startsIn: TimeSpan.FromDays(-1));
        var b = Book(t, c, u); b.RefundStatus = "REQUESTED"; b.RefundAmount = 100; t.Db.SaveChanges();
        Assert.False((await Schedule(t).DeleteClassAsync(c.Id)).Ok);
        b.RefundStatus = null; b.RefundAmount = 0; b.ChoiceNeeded = "EVENT_CANCELLED"; t.Db.SaveChanges();
        Assert.False((await Schedule(t).DeleteClassAsync(c.Id)).Ok);
    }

    [Fact]
    public async Task An_unknown_class_is_reported_as_not_found()
    {
        using var t = new TestDb();
        Assert.True((await Schedule(t).DeleteClassAsync(999)).NotFound);
    }

    [Fact]
    public async Task Clear_finished_removes_cancelled_and_past_classes_nobody_paid_for_and_leaves_the_rest()
    {
        using var t = new TestDb(); var u = t.AddUser();
        var cancelled = t.AddClass(); cancelled.Status = "CANCELLED";
        var past = t.AddClass(startsIn: TimeSpan.FromDays(-5));
        var paidPast = t.AddClass(startsIn: TimeSpan.FromDays(-5)); Book(t, paidPast, u, paid: 100);
        var upcoming = t.AddClass(); Book(t, upcoming, u);
        t.Db.SaveChanges();

        var removed = await Schedule(t).DeleteFinishedClassesAsync();

        Assert.Equal(2, removed);
        Assert.Equal([paidPast.Id, upcoming.Id], t.Db.Classes.Select(c => c.Id).OrderBy(i => i).ToList());
    }

    [Fact]
    public async Task A_plan_nobody_has_held_is_deleted_and_one_with_history_is_kept()
    {
        using var t = new TestDb(); var svc = new PlanAdminService(t.Db);
        var unused = t.AddPlan(1, 99); var used = t.AddPlan(2, 199); t.AddSubscription(t.AddUser(), used, SubscriptionStatus.Cancelled);

        Assert.True((await svc.DeleteAsync(unused.Id)).Ok);
        var (ok, _, error) = await svc.DeleteAsync(used.Id);

        Assert.False(ok); Assert.Contains("Hide it instead", error); Assert.Equal([used.Id], t.Db.Plans.Select(p => p.Id).ToList());
        Assert.True((await svc.DeleteAsync(12345)).NotFound);
    }

    [Fact]
    public async Task Deleting_a_programme_removes_its_lessons_and_the_progress_on_them_but_not_other_programmes()
    {
        using var t = new TestDb(); var u = t.AddUser();
        var gone = new FitnessProgramme { Name = "Gone", MinTier = 1 }; var stays = new FitnessProgramme { Name = "Stays", MinTier = 1 };
        t.Db.Programmes.AddRange(gone, stays); t.Db.SaveChanges();
        var l1 = new Lesson { ProgrammeId = gone.Id, Title = "a" }; var l2 = new Lesson { ProgrammeId = stays.Id, Title = "b" };
        t.Db.Lessons.AddRange(l1, l2); t.Db.SaveChanges();
        t.Db.Progress.AddRange(new Progress { UserId = u.Id, LessonId = l1.Id }, new Progress { UserId = u.Id, LessonId = l2.Id }); t.Db.SaveChanges();

        var (ok, _) = await new ProgrammeAdminService(t.Db, Schedule(t)).DeleteAsync(gone.Id);

        Assert.True(ok);
        Assert.Equal(["Stays"], t.Db.Programmes.Select(p => p.Name).ToList());
        Assert.Equal(["b"], t.Db.Lessons.Select(l => l.Title).ToList());
        Assert.Equal([l2.Id], t.Db.Progress.Select(p => p.LessonId).ToList());
    }

    private static AccountService Accounts(TestDb t)
    {
        var payFast = new PayFastService(Options.Create(new PayFastOptions { MerchantId = "1", MerchantKey = "k", Passphrase = "p" }));
        var subs = new SubscriptionService(t.Db, payFast, new FakePayFastApi(), new NotificationService(t.Db), NullLogger<SubscriptionService>.Instance);
        return new AccountService(t.Db, subs);
    }

    [Fact]
    public async Task An_administrator_can_delete_a_customer_account_and_their_personal_data()
    {
        using var t = new TestDb(); var admin = t.AddUser("Admin", Roles.Admin); var cust = t.AddUser("Test Person");
        t.Db.Journal.Add(new JournalEntry { UserId = cust.Id, Kind = "CHECKIN", Mood = "Low" });
        t.Db.Notifications.Add(new AppNotification { UserId = cust.Id, Title = "t", Body = "b", Kind = "X" }); t.Db.SaveChanges();

        var (ok, _, error) = await Accounts(t).AdminDeleteAsync(admin.Id, cust.Id);

        Assert.True(ok, error);
        Assert.DoesNotContain(t.Db.Users, u => u.Id == cust.Id);
        Assert.Empty(t.Db.Journal); Assert.Empty(t.Db.Notifications);
    }

    [Fact]
    public async Task A_customer_with_payment_history_is_erased_but_the_financial_rows_stay_without_their_details()
    {
        using var t = new TestDb(); var admin = t.AddUser("Admin", Roles.Admin); var cust = t.AddUser("Paying Person");
        var plan = t.AddPlan(1, 149); var sub = t.AddSubscription(cust, plan, SubscriptionStatus.Cancelled);
        t.Db.Payments.Add(new Payment { SubscriptionId = sub.Id, Amount = 149, Status = "COMPLETE", TransactionReference = "x" }); t.Db.SaveChanges();

        var (ok, _, _) = await Accounts(t).AdminDeleteAsync(admin.Id, cust.Id);

        Assert.True(ok); Assert.Single(t.Db.Payments);
        var row = t.Db.Users.Single(u => u.Id == cust.Id);
        Assert.Equal("Deleted user", row.FullName); Assert.Equal("DELETED", row.AccountStatus); Assert.EndsWith("@deleted.invalid", row.Email);
    }

    [Fact]
    public async Task An_administrator_cannot_delete_themselves_another_administrator_or_an_unknown_person()
    {
        using var t = new TestDb(); var admin = t.AddUser("Admin", Roles.Admin); var other = t.AddUser("Other Admin", Roles.Admin);
        var svc = Accounts(t);

        Assert.Contains("own account", (await svc.AdminDeleteAsync(admin.Id, admin.Id)).Error);
        Assert.Contains("administrator account", (await svc.AdminDeleteAsync(admin.Id, other.Id)).Error);
        Assert.True((await svc.AdminDeleteAsync(admin.Id, Guid.NewGuid())).NotFound);
        Assert.Equal(2, t.Db.Users.Count());
    }

    [Fact]
    public async Task A_switched_off_customer_can_still_be_deleted()
    {
        using var t = new TestDb(); var admin = t.AddUser("Admin", Roles.Admin); var cust = t.AddUser("Off"); cust.AccountStatus = "DISABLED"; t.Db.SaveChanges();
        Assert.True((await Accounts(t).AdminDeleteAsync(admin.Id, cust.Id)).Ok);
    }
}
