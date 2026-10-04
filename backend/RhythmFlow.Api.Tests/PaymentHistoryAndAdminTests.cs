using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

public class PlanChangeAndPaymentHistoryTests
{
    private static SubscriptionService Build(TestDb t)
    {
        var payFast = new PayFastService(Options.Create(new PayFastOptions { MerchantId = "1", MerchantKey = "k", Passphrase = "p", PublicBaseUrl = "https://api.example.com" }));
        return new SubscriptionService(t.Db, payFast, new FakePayFastApi(), new NotificationService(t.Db), NullLogger<SubscriptionService>.Instance);
    }

    [Fact]
    public async Task A_second_plan_is_refused_while_one_is_still_renewing()
    {
        using var t = new TestDb();
        var svc = Build(t);
        var user = t.AddUser();
        var flow = t.AddPlan(1, 99);
        var rhythm = t.AddPlan(2, 199);
        t.AddSubscription(user, flow, SubscriptionStatus.Active, DateTime.UtcNow.AddDays(20));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CheckoutAsync(user.Id, rhythm.Id));

        Assert.Contains("cancel it first", ex.Message);
        Assert.Equal(1, await t.Db.Subscriptions.CountAsync());   // nothing new was created
    }

    [Fact]
    public async Task A_new_plan_is_allowed_after_the_old_one_was_cancelled_even_though_access_continues()
    {
        using var t = new TestDb();
        var svc = Build(t);
        var user = t.AddUser();
        var flow = t.AddPlan(1, 99);
        var rhythm = t.AddPlan(2, 199);
        t.AddSubscription(user, flow, SubscriptionStatus.Cancelled, DateTime.UtcNow.AddDays(20));

        var response = await svc.CheckoutAsync(user.Id, rhythm.Id);

        Assert.True(response.SubscriptionId > 0);
        Assert.Equal(2, await t.Db.Subscriptions.CountAsync());
    }

    [Fact]
    public async Task Another_persons_active_plan_does_not_block_checkout()
    {
        using var t = new TestDb();
        var svc = Build(t);
        var a = t.AddUser();
        var b = t.AddUser();
        var plan = t.AddPlan();
        t.AddSubscription(a, plan, SubscriptionStatus.Active, DateTime.UtcNow.AddDays(20));

        var response = await svc.CheckoutAsync(b.Id, plan.Id);

        Assert.True(response.SubscriptionId > 0);
    }

    [Fact]
    public async Task Payment_history_lists_only_my_payments_newest_first_with_receipt_numbers()
    {
        using var t = new TestDb();
        var svc = Build(t);
        var me = t.AddUser();
        var other = t.AddUser();
        var plan = t.AddPlan(1, 99);
        var mine = t.AddSubscription(me, plan, SubscriptionStatus.Active, DateTime.UtcNow.AddDays(20));
        var theirs = t.AddSubscription(other, plan, SubscriptionStatus.Active, DateTime.UtcNow.AddDays(20));
        t.Db.Payments.Add(new Payment { SubscriptionId = mine.Id, Amount = 99, Status = "COMPLETE", PaymentDate = DateTime.UtcNow.AddDays(-40), TransactionReference = "a" });
        t.Db.Payments.Add(new Payment { SubscriptionId = mine.Id, Amount = 99, Status = "COMPLETE", PaymentDate = DateTime.UtcNow.AddDays(-10), TransactionReference = "b" });
        t.Db.Payments.Add(new Payment { SubscriptionId = theirs.Id, Amount = 99, Status = "COMPLETE", PaymentDate = DateTime.UtcNow.AddDays(-5), TransactionReference = "c" });
        await t.Db.SaveChangesAsync();

        var list = await svc.PaymentsAsync(me.Id);

        Assert.Equal(2, list.Count);
        Assert.True(list[0].Date > list[1].Date);
        Assert.All(list, p => Assert.Matches(@"^RF-\d{6}$", p.Receipt));
        Assert.All(list, p => Assert.Equal(plan.Name, p.PlanName));
    }

    [Fact]
    public async Task Payment_history_is_empty_for_someone_who_never_paid()
    {
        using var t = new TestDb();
        Assert.Empty(await Build(t).PaymentsAsync(t.AddUser().Id));
    }
}

public class UserAdminTests
{
    [Fact]
    public async Task List_hides_deleted_accounts_and_shows_the_live_plan()
    {
        using var t = new TestDb();
        var svc = new UserAdminService(t.Db);
        var alex = t.AddUser("Alex Demo");
        var gone = t.AddUser("Gone Person");
        gone.AccountStatus = "DELETED";
        t.AddSubscription(alex, t.AddPlan(2, 199), SubscriptionStatus.Active, DateTime.UtcNow.AddDays(10));
        await t.Db.SaveChangesAsync();

        var list = await svc.ListAsync(null);

        var row = Assert.Single(list);
        Assert.Equal(alex.Id, row.Id);
        Assert.Equal("Plan 2", row.Plan);
    }

    [Fact]
    public async Task Search_matches_name_username_or_email_ignoring_case()
    {
        using var t = new TestDb();
        var svc = new UserAdminService(t.Db);
        var a = t.AddUser("Alex Demo");
        t.AddUser("Someone Else");

        Assert.Single(await svc.ListAsync("ALEX"));
        Assert.Single(await svc.ListAsync(a.Email[..8].ToUpperInvariant()));
        Assert.Equal(2, (await svc.ListAsync("")).Count);
    }

    [Fact]
    public async Task Disabling_an_account_blocks_login_and_invalidates_its_tokens()
    {
        using var t = new TestDb();
        var svc = new UserAdminService(t.Db);
        var admin = t.AddUser("Admin", Roles.Admin);
        var customer = t.AddUser("Customer");
        var stampBefore = customer.SecurityStamp;

        var (ok, notFound, error) = await svc.SetStatusAsync(admin.Id, customer.Id, "DISABLED");

        Assert.True(ok); Assert.False(notFound); Assert.Null(error);
        var after = await t.Db.Users.AsNoTracking().SingleAsync(u => u.Id == customer.Id);
        Assert.Equal("DISABLED", after.AccountStatus);
        Assert.NotEqual(stampBefore, after.SecurityStamp);
    }

    [Fact]
    public async Task An_administrator_cannot_change_their_own_account()
    {
        using var t = new TestDb();
        var svc = new UserAdminService(t.Db);
        var admin = t.AddUser("Admin", Roles.Admin);

        var (ok, _, error) = await svc.SetStatusAsync(admin.Id, admin.Id, "DISABLED");

        Assert.False(ok);
        Assert.Contains("your own", error);
    }

    [Fact]
    public async Task The_only_active_administrator_cannot_be_disabled()
    {
        using var t = new TestDb();
        var svc = new UserAdminService(t.Db);
        var acting = t.AddUser("Acting", Roles.Customer);   // the caller is not an admin here, so the target is the only one
        var onlyAdmin = t.AddUser("Only Admin", Roles.Admin);

        var (ok, _, error) = await svc.SetStatusAsync(acting.Id, onlyAdmin.Id, "DISABLED");

        Assert.False(ok);
        Assert.Contains("only active administrator", error);
    }

    [Fact]
    public async Task Re_enabling_works_and_unknown_or_deleted_accounts_are_not_found()
    {
        using var t = new TestDb();
        var svc = new UserAdminService(t.Db);
        var admin = t.AddUser("Admin", Roles.Admin);
        var customer = t.AddUser("Customer");
        var deleted = t.AddUser("Deleted");
        deleted.AccountStatus = "DELETED";
        await t.Db.SaveChangesAsync();

        await svc.SetStatusAsync(admin.Id, customer.Id, "DISABLED");
        var (ok, _, _) = await svc.SetStatusAsync(admin.Id, customer.Id, "ACTIVE");
        var (_, nfDeleted, _) = await svc.SetStatusAsync(admin.Id, deleted.Id, "ACTIVE");
        var (_, nfUnknown, _) = await svc.SetStatusAsync(admin.Id, Guid.NewGuid(), "ACTIVE");

        Assert.True(ok);
        Assert.True(nfDeleted);
        Assert.True(nfUnknown);
        Assert.Equal("ACTIVE", (await t.Db.Users.AsNoTracking().SingleAsync(u => u.Id == customer.Id)).AccountStatus);
    }

    [Fact]
    public async Task A_status_other_than_active_or_disabled_is_refused()
    {
        using var t = new TestDb();
        var svc = new UserAdminService(t.Db);
        var (ok, _, error) = await svc.SetStatusAsync(t.AddUser().Id, t.AddUser().Id, "DELETED");
        Assert.False(ok);
        Assert.NotNull(error);
    }
}
