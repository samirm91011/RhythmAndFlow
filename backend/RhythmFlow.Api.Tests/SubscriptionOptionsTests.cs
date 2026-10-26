using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

/// <summary>Yearly billing, the launch price, the free trial, renewal reminders and changing plan (client feedback of 20 Oct 2026).</summary>
public class SubscriptionOptionsTests
{
    private static (SubscriptionService svc, FakePayFastApi api, EntitlementService ent) Build(TestDb t)
    {
        var api = new FakePayFastApi();
        var payFast = new PayFastService(Options.Create(new PayFastOptions { MerchantId = "1", MerchantKey = "k", Passphrase = "p" }));
        return (new SubscriptionService(t.Db, payFast, api, new NotificationService(t.Db), NullLogger<SubscriptionService>.Instance), api, new EntitlementService(t.Db));
    }

    private static SubscriptionPlan Plan(TestDb t, int tier = 1, decimal price = 149, decimal? annual = null, decimal? launch = null, DateTime? launchEnds = null, int trial = 0)
    {
        var p = t.AddPlan(tier, price);
        p.AnnualPrice = annual; p.LaunchPrice = launch; p.LaunchEndsAt = launchEnds; p.TrialDays = trial;
        t.Db.SaveChanges();
        return p;
    }

    private static string Url(CheckoutResponse r) => Uri.UnescapeDataString(r.PaymentUrl);

    // ---- Yearly billing ----

    [Fact]
    public async Task A_yearly_checkout_charges_the_yearly_price_and_asks_PayFast_for_a_yearly_cycle()
    {
        using var t = new TestDb(); var (svc, _, _) = Build(t);
        var user = t.AddUser(); var plan = Plan(t, price: 149, annual: 1490);

        var r = await svc.CheckoutAsync(user.Id, plan.Id, "ANNUAL");

        var sub = await t.Db.Subscriptions.SingleAsync();
        Assert.Equal("ANNUAL", sub.Billing);
        Assert.Contains("&amount=1490.00&", Url(r));
        Assert.Contains("&recurring_amount=1490.00&", Url(r));
        Assert.Contains("&frequency=6&", Url(r));
    }

    [Fact]
    public async Task A_monthly_checkout_still_asks_for_a_monthly_cycle()
    {
        using var t = new TestDb(); var (svc, _, _) = Build(t);
        var plan = Plan(t, price: 149, annual: 1490);

        var r = await svc.CheckoutAsync(t.AddUser().Id, plan.Id, null);

        Assert.Contains("&amount=149.00&", Url(r));
        Assert.Contains("&frequency=3&", Url(r));
    }

    [Fact]
    public async Task A_plan_without_a_yearly_price_cannot_be_bought_yearly()
    {
        using var t = new TestDb(); var (svc, _, _) = Build(t);
        var plan = Plan(t, price: 149, annual: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CheckoutAsync(t.AddUser().Id, plan.Id, "ANNUAL"));
        Assert.Contains("only available monthly", ex.Message);
    }

    [Fact]
    public async Task A_yearly_payment_covers_a_year_and_the_next_one_must_be_the_yearly_price_again()
    {
        using var t = new TestDb(); var (svc, _, _) = Build(t);
        var plan = Plan(t, price: 149, annual: 1490);
        var r = await svc.CheckoutAsync(t.AddUser().Id, plan.Id, "ANNUAL");
        var sub = await t.Db.Subscriptions.SingleAsync();

        Assert.False(await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 149m, "PF-1", null));   // the monthly price is wrong for a yearly plan
        Assert.True(await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 1490m, "PF-2", "TOKEN"));

        t.Db.Entry(sub).Reload();
        Assert.Equal(SubscriptionStatus.Active, sub.Status);
        Assert.InRange(sub.EndDate!.Value, DateTime.UtcNow.AddDays(364), DateTime.UtcNow.AddDays(367));
        Assert.Equal(1490m, SubscriptionService.ToDto((await t.Db.Subscriptions.Include(s => s.Plan).SingleAsync())).Price);
    }

    // ---- Launch price ----

    [Fact]
    public async Task A_new_subscriber_pays_the_launch_price_first_and_the_full_price_after_that()
    {
        using var t = new TestDb(); var (svc, _, _) = Build(t);
        var plan = Plan(t, price: 149, launch: 99, launchEnds: DateTime.UtcNow.AddDays(10));

        var r = await svc.CheckoutAsync(t.AddUser().Id, plan.Id);

        var sub = await t.Db.Subscriptions.SingleAsync();
        Assert.Equal(99m, sub.InitialAmount);
        Assert.Contains("&amount=99.00&", Url(r));
        Assert.Contains("&recurring_amount=149.00&", Url(r));

        Assert.False(await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 149m, "PF-1", null));   // the first payment must be the launch price
        Assert.True(await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 99m, "PF-2", "TOKEN"));
        Assert.False(await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 99m, "PF-3", null));    // later payments are the full price
        Assert.True(await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 149m, "PF-4", null));
        Assert.Equal(2, await t.Db.Payments.CountAsync());
    }

    [Fact]
    public async Task Someone_who_has_held_a_plan_before_does_not_get_the_launch_price()
    {
        using var t = new TestDb(); var (svc, _, _) = Build(t);
        var user = t.AddUser();
        var plan = Plan(t, price: 149, launch: 99, launchEnds: DateTime.UtcNow.AddDays(10));
        t.AddSubscription(user, plan, SubscriptionStatus.Cancelled, DateTime.UtcNow.AddDays(-3));

        var r = await svc.CheckoutAsync(user.Id, plan.Id);

        Assert.Contains("&amount=149.00&", Url(r));
        Assert.Equal(149m, (await t.Db.Subscriptions.OrderByDescending(s => s.Id).FirstAsync()).InitialAmount);
    }

    [Fact]
    public async Task After_the_launch_offer_ends_the_price_is_the_normal_one()
    {
        using var t = new TestDb(); var (svc, _, _) = Build(t);
        var plan = Plan(t, price: 149, launch: 99, launchEnds: DateTime.UtcNow.AddDays(-1));

        var r = await svc.CheckoutAsync(t.AddUser().Id, plan.Id);

        Assert.Contains("&amount=149.00&", Url(r));
    }

    [Fact]
    public async Task The_launch_price_is_only_shown_to_customers_while_the_offer_runs()
    {
        using var t = new TestDb();
        var running = Plan(t, price: 149, launch: 99, launchEnds: DateTime.UtcNow.AddDays(3));
        var over = Plan(t, tier: 2, price: 249, launch: 199, launchEnds: DateTime.UtcNow.AddDays(-3));

        Assert.Equal(99m, Controllers.ContentController.PlanDtoOf(running).LaunchPrice);
        Assert.Null(Controllers.ContentController.PlanDtoOf(over).LaunchPrice);
    }

    // ---- Free trial ----

    [Fact]
    public async Task A_trial_checkout_charges_nothing_now_and_starts_billing_after_the_free_days()
    {
        using var t = new TestDb(); var (svc, _, _) = Build(t);
        var plan = Plan(t, price: 149, trial: 7);

        var r = await svc.CheckoutAsync(t.AddUser().Id, plan.Id);

        var sub = await t.Db.Subscriptions.SingleAsync();
        Assert.Equal(7, sub.TrialDays);
        Assert.Equal(0m, sub.InitialAmount);
        Assert.Contains("&amount=0.00&", Url(r));
        Assert.Contains("&recurring_amount=149.00&", Url(r));
        Assert.Contains($"&billing_date={DateTime.UtcNow.Date.AddDays(7):yyyy-MM-dd}&", Url(r));
    }

    [Fact]
    public async Task The_trial_gives_access_straight_away_records_no_payment_and_then_the_first_charge_starts_the_paid_period()
    {
        using var t = new TestDb(); var (svc, _, ent) = Build(t);
        var user = t.AddUser(); var plan = Plan(t, tier: 2, price: 249, trial: 7);
        await svc.CheckoutAsync(user.Id, plan.Id);
        var sub = await t.Db.Subscriptions.SingleAsync();

        Assert.True(await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 0m, "PF-T", "TOKEN"));

        t.Db.Entry(sub).Reload();
        Assert.Equal(SubscriptionStatus.Active, sub.Status);
        Assert.True(sub.AwaitingFirstCharge);
        Assert.InRange(sub.EndDate!.Value, DateTime.UtcNow.AddDays(6), DateTime.UtcNow.AddDays(8));
        Assert.Equal(0, await t.Db.Payments.CountAsync());
        Assert.Equal(2, await ent.GetUserTierAsync(user.Id));
        Assert.Contains(t.Db.Notifications, n => n.UserId == user.Id && n.Title.Contains("free trial"));

        Assert.True(await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 249m, "PF-C", null));   // PayFast's first real charge, at the end of the trial
        t.Db.Entry(sub).Reload();
        Assert.False(sub.AwaitingFirstCharge);
        Assert.InRange(sub.EndDate!.Value, DateTime.UtcNow.AddDays(27), DateTime.UtcNow.AddDays(32));
        Assert.Equal(1, await t.Db.Payments.CountAsync());
    }

    [Fact]
    public async Task A_zero_payment_is_refused_when_the_checkout_expected_money_and_a_repeated_zero_notice_is_harmless()
    {
        using var t = new TestDb(); var (svc, _, _) = Build(t);
        var plan = Plan(t, price: 149);
        await svc.CheckoutAsync(t.AddUser().Id, plan.Id);
        var sub = await t.Db.Subscriptions.SingleAsync();
        Assert.False(await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 0m, "PF-0", null));
        t.Db.Entry(sub).Reload();
        Assert.Equal(SubscriptionStatus.Pending, sub.Status);

        var trialPlan = Plan(t, tier: 2, price: 249, trial: 7);
        await svc.CheckoutAsync(t.AddUser().Id, trialPlan.Id);
        var trialSub = await t.Db.Subscriptions.OrderByDescending(s => s.Id).FirstAsync();
        Assert.True(await svc.ApplyPaymentResultAsync(trialSub.Id, "COMPLETE", 0m, "PF-1", "TOKEN"));
        Assert.True(await svc.ApplyPaymentResultAsync(trialSub.Id, "COMPLETE", 0m, "PF-2", "TOKEN"));   // PayFast repeating the notice
        t.Db.Entry(trialSub).Reload();
        Assert.True(trialSub.AwaitingFirstCharge);
    }

    [Fact]
    public async Task The_trial_is_only_for_people_who_have_never_held_a_plan()
    {
        using var t = new TestDb(); var (svc, _, _) = Build(t);
        var user = t.AddUser(); var plan = Plan(t, price: 149, trial: 7);
        t.AddSubscription(user, plan, SubscriptionStatus.Expired, DateTime.UtcNow.AddDays(-40));

        var r = await svc.CheckoutAsync(user.Id, plan.Id);

        Assert.Contains("&amount=149.00&", Url(r));
        Assert.Equal(0, (await t.Db.Subscriptions.OrderByDescending(s => s.Id).FirstAsync()).TrialDays);
    }

    // ---- Changing plan ----

    private static async Task<(Subscription sub, SubscriptionPlan basic, SubscriptionPlan plus, User user)> ActiveOnBasic(TestDb t, int daysLeft = 20)
    {
        var user = t.AddUser();
        var basic = Plan(t, tier: 1, price: 149, annual: 1490);
        var plus = Plan(t, tier: 2, price: 249, annual: 2490);
        var sub = t.AddSubscription(user, basic, SubscriptionStatus.Active, DateTime.UtcNow.AddDays(daysLeft), token: "TOKEN-OLD");
        await Task.CompletedTask;
        return (sub, basic, plus, user);
    }

    [Fact]
    public async Task The_preview_shows_the_price_difference_the_start_day_and_what_happens_to_access()
    {
        using var t = new TestDb(); var (svc, _, _) = Build(t);
        var (sub, _, plus, user) = await ActiveOnBasic(t);

        var (preview, error) = await svc.PreviewChangeAsync(user.Id, sub.Id, plus.Id, "MONTHLY");

        Assert.Null(error);
        Assert.Equal(149m, preview!.CurrentPrice); Assert.Equal(249m, preview.NewPrice);
        Assert.Equal(100m, preview.PriceDifference);
        Assert.Equal(sub.EndDate, preview.EffectiveDate);
        Assert.Contains("move up to", preview.AccessImpact);
        Assert.Equal(SubscriptionStatus.Active, (await t.Db.Subscriptions.SingleAsync()).Status);   // a preview changes nothing
    }

    [Fact]
    public async Task A_downgrade_preview_warns_that_higher_level_programmes_will_lock()
    {
        using var t = new TestDb(); var (svc, _, _) = Build(t);
        var (sub, basic, plus, user) = await ActiveOnBasic(t);
        sub.PlanId = plus.Id; t.Db.SaveChanges();

        var (preview, _) = await svc.PreviewChangeAsync(user.Id, sub.Id, basic.Id, "MONTHLY");

        Assert.Equal(-100m, preview!.PriceDifference);
        Assert.Contains("locked", preview.AccessImpact);
    }

    [Fact]
    public async Task Moving_to_yearly_billing_compares_prices_per_month()
    {
        using var t = new TestDb(); var (svc, _, _) = Build(t);
        var (sub, basic, _, user) = await ActiveOnBasic(t);

        var (preview, _) = await svc.PreviewChangeAsync(user.Id, sub.Id, basic.Id, "ANNUAL");

        Assert.Equal(1490m, preview!.NewPrice);
        Assert.Equal(Math.Round(1490m / 12m - 149m, 2), preview.PriceDifference);
    }

    [Fact]
    public async Task Confirming_a_change_stops_the_old_renewal_and_sets_the_new_plan_to_start_when_the_paid_period_ends()
    {
        using var t = new TestDb(); var (svc, api, ent) = Build(t);
        var (sub, _, plus, user) = await ActiveOnBasic(t);
        var end = sub.EndDate!.Value;

        var (result, error) = await svc.ChangePlanAsync(user.Id, sub.Id, plus.Id, "MONTHLY");

        Assert.Null(error);
        Assert.Equal("TOKEN-OLD", api.LastToken);                                    // PayFast was told to stop the old plan
        var old = await t.Db.Subscriptions.FindAsync(sub.Id);
        Assert.Equal(SubscriptionStatus.Cancelled, old!.Status);
        Assert.Equal(end, old.EndDate);                                              // paid days are kept
        var fresh = await t.Db.Subscriptions.SingleAsync(s => s.Id == result!.SubscriptionId);
        Assert.Equal(SubscriptionStatus.Pending, fresh.Status);
        Assert.Equal(end, fresh.PlannedStart);
        Assert.Equal(0m, fresh.InitialAmount);
        Assert.Contains("&amount=0.00&", Url(result!));
        Assert.Contains($"&billing_date={end:yyyy-MM-dd}&", Url(result!));
        Assert.Equal(1, await ent.GetUserTierAsync(user.Id));                         // still the old plan until then
    }

    [Fact]
    public async Task A_plan_that_starts_later_gives_no_access_until_its_start_day_and_is_then_charged_once_PayFast_bills_it()
    {
        using var t = new TestDb(); var (svc, _, ent) = Build(t);
        var (sub, _, plus, user) = await ActiveOnBasic(t);
        var (result, _) = await svc.ChangePlanAsync(user.Id, sub.Id, plus.Id, "MONTHLY");

        Assert.True(await svc.ApplyPaymentResultAsync(result!.SubscriptionId, "COMPLETE", 0m, "PF-Z", "TOKEN-NEW"));

        Assert.Equal(1, await ent.GetUserTierAsync(user.Id));                         // the higher plan has not started yet
        var fresh = await t.Db.Subscriptions.SingleAsync(s => s.Id == result.SubscriptionId);
        Assert.Equal(SubscriptionStatus.Active, fresh.Status);
        Assert.True(fresh.StartDate > DateTime.UtcNow.AddDays(10));

        // the day arrives: pretend the start is now and PayFast takes the first real payment
        fresh.StartDate = DateTime.UtcNow.AddMinutes(-5); fresh.EndDate = DateTime.UtcNow.AddHours(20); t.Db.SaveChanges();
        Assert.True(await svc.ApplyPaymentResultAsync(fresh.Id, "COMPLETE", 249m, "PF-R", null));
        t.Db.Entry(fresh).Reload();
        Assert.InRange(fresh.EndDate!.Value, DateTime.UtcNow.AddDays(27), DateTime.UtcNow.AddDays(32));
        Assert.Equal(2, await ent.GetUserTierAsync(user.Id));
    }

    [Fact]
    public async Task A_change_is_refused_if_PayFast_cannot_stop_the_old_renewal_and_nothing_is_cancelled()
    {
        using var t = new TestDb(); var (svc, api, _) = Build(t);
        var (sub, _, plus, user) = await ActiveOnBasic(t);
        api.Succeeds = false;

        var (result, error) = await svc.ChangePlanAsync(user.Id, sub.Id, plus.Id, "MONTHLY");

        Assert.Null(result); Assert.NotNull(error);
        Assert.Equal(SubscriptionStatus.Active, (await t.Db.Subscriptions.FindAsync(sub.Id))!.Status);
        Assert.Equal(1, await t.Db.Subscriptions.CountAsync());
    }

    [Fact]
    public async Task Changing_to_the_plan_you_already_have_or_when_the_period_ends_tomorrow_is_refused()
    {
        using var t = new TestDb(); var (svc, _, _) = Build(t);
        var (sub, basic, plus, user) = await ActiveOnBasic(t);

        var (_, same) = await svc.PreviewChangeAsync(user.Id, sub.Id, basic.Id, "MONTHLY");
        Assert.Contains("already have", same);

        sub.EndDate = DateTime.UtcNow.AddHours(5); t.Db.SaveChanges();
        var (_, soon) = await svc.PreviewChangeAsync(user.Id, sub.Id, plus.Id, "MONTHLY");
        Assert.Contains("ends very soon", soon);

        var (_, notYours) = await svc.PreviewChangeAsync(Guid.NewGuid(), sub.Id, plus.Id, "MONTHLY");
        Assert.Contains("active plan", notYours);
    }

    [Fact]
    public async Task The_old_plan_getting_replaced_does_not_trigger_an_access_ends_warning()
    {
        using var t = new TestDb(); var (svc, _, _) = Build(t);
        var (sub, _, plus, user) = await ActiveOnBasic(t, daysLeft: 2);
        await svc.ChangePlanAsync(user.Id, sub.Id, plus.Id, "MONTHLY");

        await svc.SendRemindersAsync(DateTime.UtcNow);

        Assert.DoesNotContain(t.Db.Notifications, n => n.Title == "Your access ends soon");
    }

    // ---- Reminders ----

    [Fact]
    public async Task A_monthly_plan_is_reminded_a_few_days_before_it_renews_and_only_once()
    {
        using var t = new TestDb(); var (svc, _, _) = Build(t);
        var user = t.AddUser(); var plan = Plan(t, price: 149);
        t.AddSubscription(user, plan, SubscriptionStatus.Active, DateTime.UtcNow.AddDays(2), token: "TOKEN");

        await svc.SendRemindersAsync(DateTime.UtcNow);
        await svc.SendRemindersAsync(DateTime.UtcNow);

        var reminder = Assert.Single(t.Db.Notifications.Where(n => n.UserId == user.Id));
        Assert.Contains("renews", reminder.Title);
        Assert.Contains("R149.00", reminder.Body);
    }

    [Fact]
    public async Task A_yearly_plan_gets_its_reminder_a_fortnight_ahead_but_a_monthly_one_does_not()
    {
        using var t = new TestDb(); var (svc, _, _) = Build(t);
        var yearly = t.AddUser(); var monthly = t.AddUser(); var plan = Plan(t, price: 149, annual: 1490);
        var y = t.AddSubscription(yearly, plan, SubscriptionStatus.Active, DateTime.UtcNow.AddDays(10), token: "T1"); y.Billing = "ANNUAL";
        t.AddSubscription(monthly, plan, SubscriptionStatus.Active, DateTime.UtcNow.AddDays(10), token: "T2");
        t.Db.SaveChanges();

        await svc.SendRemindersAsync(DateTime.UtcNow);

        Assert.Contains(t.Db.Notifications, n => n.UserId == yearly.Id && n.Title.Contains("yearly"));
        Assert.DoesNotContain(t.Db.Notifications, n => n.UserId == monthly.Id);
    }

    [Fact]
    public async Task A_cancelled_plan_that_is_about_to_end_gets_an_access_ends_notice_and_a_trial_gets_a_trial_ends_notice()
    {
        using var t = new TestDb(); var (svc, _, _) = Build(t);
        var plan = Plan(t, price: 149, trial: 7);
        var cancelled = t.AddUser(); var trial = t.AddUser();
        t.AddSubscription(cancelled, plan, SubscriptionStatus.Cancelled, DateTime.UtcNow.AddDays(2));
        var ts = t.AddSubscription(trial, plan, SubscriptionStatus.Active, DateTime.UtcNow.AddDays(1), token: "T3");
        ts.AwaitingFirstCharge = true; ts.TrialDays = 7; t.Db.SaveChanges();

        await svc.SendRemindersAsync(DateTime.UtcNow);

        Assert.Contains(t.Db.Notifications, n => n.UserId == cancelled.Id && n.Title == "Your access ends soon");
        Assert.Contains(t.Db.Notifications, n => n.UserId == trial.Id && n.Title.Contains("free trial ends"));
    }

    [Fact]
    public async Task A_trial_that_has_run_out_without_a_charge_is_not_marked_overdue_by_the_sweep()
    {
        using var t = new TestDb(); var (svc, _, _) = Build(t);
        var plan = Plan(t, price: 149, trial: 7);
        var s = t.AddSubscription(t.AddUser(), plan, SubscriptionStatus.Active, DateTime.UtcNow.AddHours(-1), token: "T4");
        s.AwaitingFirstCharge = true; t.Db.SaveChanges();

        await svc.SweepExpiredAsync();

        t.Db.Entry(s).Reload();
        Assert.Equal(SubscriptionStatus.Active, s.Status);   // PayFast's first charge may still be on its way
    }

    // ---- What the customer sees ----

    [Fact]
    public async Task My_subscription_shows_the_billing_period_the_price_for_it_and_whether_it_renews()
    {
        using var t = new TestDb(); var (svc, _, _) = Build(t);
        var user = t.AddUser(); var plan = Plan(t, price: 149, annual: 1490);
        var s = t.AddSubscription(user, plan, SubscriptionStatus.Active, DateTime.UtcNow.AddDays(100), token: "TOKEN"); s.Billing = "ANNUAL"; t.Db.SaveChanges();

        var dto = Assert.Single(await svc.MineAsync(user.Id));

        Assert.Equal("ANNUAL", dto.Billing);
        Assert.Equal(1490m, dto.Price);
        Assert.True(dto.Renewing);
        s.Status = SubscriptionStatus.Cancelled; t.Db.SaveChanges();
        Assert.False(Assert.Single(await svc.MineAsync(user.Id)).Renewing);
    }
}

public class PlanOptionsAdminTests
{
    private static PlanUpsert Plan(decimal price = 149, decimal? annual = null, decimal? launch = null, DateTime? ends = null, int trial = 0) =>
        new("Studio", "A test plan", price, 1, "One", "ACTIVE", annual, launch, ends, trial);

    [Fact]
    public async Task The_yearly_price_launch_offer_and_trial_days_are_saved_and_listed()
    {
        using var t = new TestDb(); var svc = new PlanAdminService(t.Db);
        var ends = DateTime.UtcNow.AddDays(14);

        var (ok, id, error) = await svc.CreateAsync(Plan(149, 1490, 99, ends, 7));

        Assert.True(ok, error);
        var row = (await svc.ListAsync()).Single(p => p.Id == id);
        Assert.Equal(1490m, row.AnnualPrice); Assert.Equal(99m, row.LaunchPrice); Assert.Equal(7, row.TrialDays);
        Assert.NotNull(row.LaunchEndsAt);
    }

    [Theory]
    [InlineData(149, 4.0, null, 0, "yearly price must be at least R5")]
    [InlineData(149, null, 4.0, 0, "launch price must be at least R5")]
    [InlineData(149, null, 149.0, 0, "lower than the normal monthly price")]
    [InlineData(149, null, 99.0, 0, "day the launch price ends")]
    [InlineData(149, null, null, 61, "between 0 and 60")]
    public async Task Invalid_offer_settings_are_refused(decimal price, double? annual, double? launch, int trial, string expected)
    {
        using var t = new TestDb(); var svc = new PlanAdminService(t.Db);

        var (ok, _, error) = await svc.CreateAsync(Plan(price, (decimal?)annual, (decimal?)launch, null, trial));

        Assert.False(ok);
        Assert.Contains(expected, error);
    }

    [Fact]
    public async Task Clearing_the_launch_price_also_clears_its_end_day()
    {
        using var t = new TestDb(); var svc = new PlanAdminService(t.Db);
        var (_, id, _) = await svc.CreateAsync(Plan(149, null, 99, DateTime.UtcNow.AddDays(5)));

        await svc.UpdateAsync(id, Plan(149, null, null, DateTime.UtcNow.AddDays(5)));

        var row = (await svc.ListAsync()).Single();
        Assert.Null(row.LaunchPrice); Assert.Null(row.LaunchEndsAt);
    }
}

public class ClientPlanDataTests
{
    [Fact]
    public async Task A_fresh_database_gets_the_two_client_plans_with_a_yearly_price_and_the_structured_programme_on_the_second()
    {
        using var t = new TestDb();
        await SeedData.RunAsync(t.Db, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        var plans = (await t.Db.Plans.AsNoTracking().ToListAsync()).OrderBy(p => p.Tier).ToList();
        Assert.Equal(["Move & Breathe", "Move, Breathe & Grow"], plans.Select(p => p.Name).ToArray());
        Assert.Equal([149m, 249m], plans.Select(p => p.Price).ToArray());
        Assert.Equal([1490m, 2490m], plans.Select(p => p.AnnualPrice!.Value).ToArray());
        Assert.Equal(2, (await t.Db.Programmes.SingleAsync(p => p.Name == "7-Day Flow Plan")).MinTier);
    }

    [Fact]
    public async Task An_older_database_with_the_placeholder_plans_is_turned_into_the_client_plans_once()
    {
        using var t = new TestDb();
        t.Db.Plans.AddRange(
            new SubscriptionPlan { Name = "Flow", Price = 99, Tier = 1, Description = "old", Features = "old" },
            new SubscriptionPlan { Name = "Rhythm", Price = 199, Tier = 2, Description = "old", Features = "old" },
            new SubscriptionPlan { Name = "Rhythm & Flow Unlimited", Price = 299, Tier = 3, Description = "old" });
        t.Db.Programmes.AddRange(
            new FitnessProgramme { Name = "Full Body Flow Masterclass", MinTier = 3 },
            new FitnessProgramme { Name = "Mindful Mobility", MinTier = 2 });
        await t.Db.SaveChangesAsync();

        await SchemaUpgrades.ApplyAsync(t.Db);
        await SchemaUpgrades.ApplyAsync(t.Db);

        var plans = (await t.Db.Plans.AsNoTracking().ToListAsync()).OrderBy(p => p.Id).ToList();
        Assert.Equal("Move & Breathe", plans[0].Name); Assert.Equal(149m, plans[0].Price); Assert.Equal(1490m, plans[0].AnnualPrice);
        Assert.Contains("Full on-demand video library", plans[0].Features);
        Assert.Equal("Move, Breathe & Grow", plans[1].Name); Assert.Equal(249m, plans[1].Price);
        Assert.Equal("INACTIVE", plans[2].Status);                                    // hidden, not deleted
        var progs = await t.Db.Programmes.AsNoTracking().ToListAsync();
        Assert.Equal(2, progs.Single(p => p.Name == "7-Day Flow Plan").MinTier);
        Assert.Equal(1, progs.Single(p => p.Name == "Mindful Mobility").MinTier);
    }

    [Fact]
    public async Task A_plan_an_administrator_already_renamed_is_left_alone()
    {
        using var t = new TestDb();
        t.Db.Plans.Add(new SubscriptionPlan { Name = "Studio Pass", Price = 175, Tier = 1 });
        await t.Db.SaveChangesAsync();

        await SchemaUpgrades.ApplyAsync(t.Db);

        var plan = await t.Db.Plans.AsNoTracking().SingleAsync();
        Assert.Equal("Studio Pass", plan.Name); Assert.Equal(175m, plan.Price);
    }

    [Fact]
    public async Task A_database_from_before_the_new_columns_gets_them_and_keeps_its_data()
    {
        using var t = new TestDb();
        var plan = t.AddPlan(1, 149); var user = t.AddUser(); t.AddSubscription(user, plan, SubscriptionStatus.Active, DateTime.UtcNow.AddDays(5));
        // an older database does not have these columns (SQLite 3.35+ can drop a column)
        await t.Db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Plans\" DROP COLUMN \"AnnualPrice\"");
        await t.Db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Subscriptions\" DROP COLUMN \"Billing\"");

        await SchemaUpgrades.ApplyAsync(t.Db);

        Assert.Equal("MONTHLY", (await t.Db.Subscriptions.AsNoTracking().SingleAsync()).Billing);
        Assert.Null((await t.Db.Plans.AsNoTracking().SingleAsync()).AnnualPrice);
    }
}

public class PolicyContentTests
{
    [Fact]
    public void The_four_policies_are_present_with_the_clients_headings_and_are_found_by_id()
    {
        Assert.Equal(["subscription-cancellation", "subscription-renewal", "event-cancellation", "online-technical"],
            PolicyContent.All.Select(p => p.Id).ToArray());
        Assert.All(PolicyContent.All, p => { Assert.False(string.IsNullOrWhiteSpace(p.Title)); Assert.True(p.Body.Length > 500); });
        Assert.Contains("48 hours", PolicyContent.Find("event-cancellation")!.Body);
        Assert.Contains("12 months", PolicyContent.Find("event-cancellation")!.Body);
        Assert.Null(PolicyContent.Find("nope"));
    }
}
