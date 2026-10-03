using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

public class PayFastServiceTests
{
    private static PayFastService Service(string passphrase = "pass phrase", bool sandbox = true) =>
        new(Options.Create(new PayFastOptions { MerchantId = "10000100", MerchantKey = "key123", Passphrase = passphrase, Sandbox = sandbox, PublicBaseUrl = "https://api.example.com" }));

    private static (User user, SubscriptionPlan plan, Subscription sub) Sample() =>
        (new User { FullName = "Alex Demo", Email = "alex@example.com" }, new SubscriptionPlan { Name = "Rhythm & Flow", Price = 199 }, new Subscription { Id = 42 });

    [Fact]
    public void UrlEncode_matches_PHP_urlencode_which_PayFast_uses()
    {
        Assert.Equal("Rhythm+%26+Flow", PayFastService.UrlEncode("Rhythm & Flow"));
        Assert.Equal("a%40b.com", PayFastService.UrlEncode("a@b.com"));
        Assert.Equal("http%3A%2F%2Fx.co%2Fa%3Fb%3D1", PayFastService.UrlEncode("http://x.co/a?b=1"));
        Assert.Equal("caf%C3%A9", PayFastService.UrlEncode("café"));
    }

    [Fact]
    public void Payment_link_targets_the_sandbox_and_is_signed_when_a_passphrase_is_set()
    {
        var (u, p, s) = Sample();
        var url = Service().BuildPaymentUrl(u, p, s);
        Assert.StartsWith("https://sandbox.payfast.co.za/eng/process?", url);
        Assert.Contains("amount=199.00", url);
        Assert.Contains("subscription_type=1", url);   // recurring
        Assert.Contains("frequency=3", url);           // monthly
        Assert.Contains("m_payment_id=42", url);
        Assert.Contains("notify_url=https%3A%2F%2Fapi.example.com%2Fapi%2Fpayfast%2Fitn", url);
        Assert.Matches("signature=[0-9a-f]{32}$", url);
    }

    [Fact]
    public void Live_mode_uses_the_live_host()
    {
        var (u, p, s) = Sample();
        Assert.StartsWith("https://www.payfast.co.za/eng/process?", Service(sandbox: false).BuildPaymentUrl(u, p, s));
    }

    [Fact]
    public void Sandbox_without_a_passphrase_sends_an_unsigned_link_for_the_shared_test_merchant()
    {
        var (u, p, s) = Sample();
        Assert.DoesNotContain("signature=", Service(passphrase: "").BuildPaymentUrl(u, p, s));
    }

    [Fact]
    public void Live_mode_always_signs_requests()
    {
        var (u, p, s) = Sample();
        Assert.Contains("signature=", Service(passphrase: "x", sandbox: false).BuildPaymentUrl(u, p, s));
    }

    [Fact]
    public void A_genuine_notification_verifies_and_a_tampered_one_does_not()
    {
        var (u, p, s) = Sample();
        var svc = Service();
        var url = svc.BuildPaymentUrl(u, p, s);
        var query = url[(url.IndexOf('?') + 1)..];
        var pairs = query.Split('&').Select(x => x.Split('=', 2))
            .Select(x => new KeyValuePair<string, string>(x[0], WebUtility.UrlDecode(x[1]))).ToList();
        var signature = pairs.Single(x => x.Key == "signature").Value;

        Assert.True(svc.VerifySignature(pairs, signature));

        var tampered = pairs.Select(x => x.Key == "amount" ? new KeyValuePair<string, string>("amount", "1.00") : x).ToList();
        Assert.False(svc.VerifySignature(tampered, signature));
        Assert.False(Service("other passphrase").VerifySignature(pairs, signature));
    }
}

public class MediaTokenTests
{
    private static MediaTokenService Service(int minutes = 60) =>
        new(Options.Create(new MediaOptions { SigningKey = new string('k', 40), LinkMinutes = minutes }));

    private static (Guid user, long exp, string sig) Parse(string query)
    {
        var d = query.Split('&').Select(x => x.Split('=')).ToDictionary(x => x[0], x => x[1]);
        return (Guid.Parse(d["u"]), long.Parse(d["e"]), d["s"]);
    }

    [Fact]
    public void A_fresh_link_is_valid_for_the_right_user_and_lesson()
    {
        var svc = Service(); var user = Guid.NewGuid();
        var (u, e, s) = Parse(svc.Sign(7, user).Query);
        Assert.True(svc.Validate(7, u, e, s));
    }

    [Fact]
    public void A_link_cannot_be_reused_for_another_lesson_or_user()
    {
        var svc = Service(); var user = Guid.NewGuid();
        var (u, e, s) = Parse(svc.Sign(7, user).Query);
        Assert.False(svc.Validate(8, u, e, s));
        Assert.False(svc.Validate(7, Guid.NewGuid(), e, s));
    }

    [Fact]
    public void An_expired_link_is_rejected()
    {
        var svc = Service(minutes: -1); var user = Guid.NewGuid();
        var (u, e, s) = Parse(svc.Sign(7, user).Query);
        Assert.False(svc.Validate(7, u, e, s));
    }

    [Fact]
    public void A_tampered_signature_or_expiry_is_rejected()
    {
        var svc = Service(); var user = Guid.NewGuid();
        var (u, e, s) = Parse(svc.Sign(7, user).Query);
        Assert.False(svc.Validate(7, u, e, new string('0', s.Length)));
        Assert.False(svc.Validate(7, u, e + 3600, s)); // trying to extend the expiry
    }
}

public class PasswordResetTests
{
    private static readonly PasswordHasher<User> Hasher = new();

    private static (PasswordResetService svc, FakeEmail mail, User user, TestDb t) Setup()
    {
        var t = new TestDb(); var mail = new FakeEmail();
        var user = t.AddUser("Reset User");
        user.PasswordHash = Hasher.HashPassword(user, "OldPassw0rd!"); t.Db.SaveChanges();
        var svc = new PasswordResetService(t.Db, mail, Options.Create(new JwtOptions { Key = new string('j', 40) }), NullLogger<PasswordResetService>.Instance);
        return (svc, mail, user, t);
    }

    private static string CodeIn(FakeEmail mail) =>
        System.Text.RegularExpressions.Regex.Match(mail.Sent.Last().Body, @"\b\d{6}\b").Value;

    [Fact]
    public async Task A_valid_code_changes_the_password_and_signs_out_other_sessions()
    {
        var (svc, mail, user, t) = Setup(); using var _ = t;
        var oldStamp = user.SecurityStamp;
        await svc.RequestAsync(user.Email);
        var (ok, error) = await svc.ResetAsync(user.Email, CodeIn(mail), "BrandNewPass1!");
        Assert.True(ok, error);
        t.Db.Entry(user).Reload();
        Assert.Equal(PasswordVerificationResult.Success, Hasher.VerifyHashedPassword(user, user.PasswordHash, "BrandNewPass1!"));
        Assert.Equal(PasswordVerificationResult.Failed, Hasher.VerifyHashedPassword(user, user.PasswordHash, "OldPassw0rd!"));
        Assert.NotEqual(oldStamp, user.SecurityStamp);
    }

    [Fact]
    public async Task A_code_works_only_once()
    {
        var (svc, mail, user, t) = Setup(); using var _ = t;
        await svc.RequestAsync(user.Email);
        var code = CodeIn(mail);
        Assert.True((await svc.ResetAsync(user.Email, code, "BrandNewPass1!")).Ok);
        Assert.False((await svc.ResetAsync(user.Email, code, "AnotherPass1!")).Ok);
    }

    [Fact]
    public async Task A_wrong_code_is_rejected()
    {
        var (svc, mail, user, t) = Setup(); using var _ = t;
        await svc.RequestAsync(user.Email);
        var wrong = CodeIn(mail) == "000000" ? "111111" : "000000";
        Assert.False((await svc.ResetAsync(user.Email, wrong, "BrandNewPass1!")).Ok);
    }

    [Fact]
    public async Task After_five_wrong_guesses_even_the_right_code_is_locked_out()
    {
        var (svc, mail, user, t) = Setup(); using var _ = t;
        await svc.RequestAsync(user.Email);
        var right = CodeIn(mail); var wrong = right == "000000" ? "111111" : "000000";
        for (var i = 0; i < 5; i++) await svc.ResetAsync(user.Email, wrong, "BrandNewPass1!");
        Assert.False((await svc.ResetAsync(user.Email, right, "BrandNewPass1!")).Ok);
    }

    [Fact]
    public async Task An_expired_code_is_rejected()
    {
        var (svc, mail, user, t) = Setup(); using var _ = t;
        await svc.RequestAsync(user.Email);
        var entry = t.Db.PasswordResets.Single(); entry.ExpiresAt = DateTime.UtcNow.AddMinutes(-1); t.Db.SaveChanges();
        Assert.False((await svc.ResetAsync(user.Email, CodeIn(mail), "BrandNewPass1!")).Ok);
    }

    [Fact]
    public async Task Only_the_newest_code_works()
    {
        var (svc, mail, user, t) = Setup(); using var _ = t;
        await svc.RequestAsync(user.Email); var first = CodeIn(mail);
        await svc.RequestAsync(user.Email); var second = CodeIn(mail);
        if (first != second) Assert.False((await svc.ResetAsync(user.Email, first, "BrandNewPass1!")).Ok);
        Assert.True((await svc.ResetAsync(user.Email, second, "BrandNewPass1!")).Ok);
    }

    [Fact]
    public async Task Unknown_emails_get_no_email_and_no_error_so_accounts_cannot_be_discovered()
    {
        var (svc, mail, _, t) = Setup(); using var _t = t;
        await svc.RequestAsync("nobody@example.com");
        Assert.Empty(mail.Sent);
    }

    [Fact]
    public async Task Reset_requests_are_limited_to_three_per_hour()
    {
        var (svc, mail, user, t) = Setup(); using var _ = t;
        for (var i = 0; i < 6; i++) await svc.RequestAsync(user.Email);
        Assert.Equal(3, mail.Sent.Count);
    }
}

public class SubscriptionTests
{
    private static (SubscriptionService svc, FakePayFastApi api) Build(TestDb t)
    {
        var api = new FakePayFastApi();
        var payFast = new PayFastService(Options.Create(new PayFastOptions { MerchantId = "1", MerchantKey = "k", Passphrase = "p" }));
        return (new SubscriptionService(t.Db, payFast, api, new NotificationService(t.Db), NullLogger<SubscriptionService>.Instance), api);
    }

    [Fact]
    public async Task A_confirmed_payment_activates_the_subscription_for_a_month_and_notifies_the_customer()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var user = t.AddUser(); var plan = t.AddPlan(tier: 2, price: 199);
        var sub = t.AddSubscription(user, plan, SubscriptionStatus.Pending);
        Assert.True(await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 199m, "PF-1", "TOKEN-1"));
        t.Db.Entry(sub).Reload();
        Assert.Equal(SubscriptionStatus.Active, sub.Status);
        Assert.Equal("TOKEN-1", sub.PayFastReference);
        Assert.InRange(sub.EndDate!.Value, DateTime.UtcNow.AddDays(27), DateTime.UtcNow.AddDays(32));
        Assert.Single(t.Db.Payments);
        Assert.Contains(t.Db.Notifications, n => n.UserId == user.Id && n.Title.Contains("subscribed"));
    }

    [Fact]
    public async Task A_payment_for_the_wrong_amount_is_refused_and_nothing_is_activated()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var sub = t.AddSubscription(t.AddUser(), t.AddPlan(price: 199), SubscriptionStatus.Pending);
        Assert.False(await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 1m, "PF-1", null));
        t.Db.Entry(sub).Reload();
        Assert.Equal(SubscriptionStatus.Pending, sub.Status);
        Assert.Empty(t.Db.Payments);
    }

    [Fact]
    public async Task The_same_notification_delivered_twice_is_applied_once()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var sub = t.AddSubscription(t.AddUser(), t.AddPlan(price: 99), SubscriptionStatus.Pending);
        await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 99m, "PF-1", null);
        await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 99m, "PF-1", null);
        Assert.Single(t.Db.Payments);
    }

    [Fact]
    public async Task A_recurring_payment_extends_from_the_current_end_date()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var end = DateTime.UtcNow.AddDays(10);
        var sub = t.AddSubscription(t.AddUser(), t.AddPlan(price: 99), SubscriptionStatus.Active, end);
        await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 99m, "PF-2", null);
        t.Db.Entry(sub).Reload();
        Assert.InRange(sub.EndDate!.Value, end.AddMonths(1).AddMinutes(-1), end.AddMonths(1).AddMinutes(1));
    }

    [Fact]
    public async Task A_failed_payment_makes_an_active_subscription_past_due_and_a_pending_one_failed()
    {
        using var t = new TestDb(); var (svc, _) = Build(t); var plan = t.AddPlan(price: 99);
        var active = t.AddSubscription(t.AddUser(), plan, SubscriptionStatus.Active, DateTime.UtcNow.AddDays(1));
        var pending = t.AddSubscription(t.AddUser(), plan, SubscriptionStatus.Pending);
        await svc.ApplyPaymentResultAsync(active.Id, "FAILED", 99m, "PF-A", null);
        await svc.ApplyPaymentResultAsync(pending.Id, "FAILED", 99m, "PF-B", null);
        t.Db.Entry(active).Reload(); t.Db.Entry(pending).Reload();
        Assert.Equal(SubscriptionStatus.PastDue, active.Status);
        Assert.Equal(SubscriptionStatus.Failed, pending.Status);
    }

    [Fact]
    public async Task Cancelling_a_subscription_linked_to_PayFast_tells_PayFast_and_keeps_access_until_the_end_date()
    {
        using var t = new TestDb(); var (svc, api) = Build(t);
        var user = t.AddUser(); var end = DateTime.UtcNow.AddDays(12);
        var sub = t.AddSubscription(user, t.AddPlan(), SubscriptionStatus.Active, end, token: "real-token-123");
        var (ok, result, error) = await svc.CancelAsync(user.Id, sub.Id);
        Assert.True(ok, error);
        Assert.True(result!.CancelledWithPayFast);
        Assert.Equal("real-token-123", api.LastToken);
        t.Db.Entry(sub).Reload();
        Assert.Equal(SubscriptionStatus.Cancelled, sub.Status);
        Assert.True(EntitlementService.GrantsAccess(sub));
    }

    [Fact]
    public async Task If_PayFast_cannot_cancel_nothing_changes_and_the_customer_is_told_to_retry()
    {
        using var t = new TestDb(); var (svc, api) = Build(t); api.Succeeds = false;
        var user = t.AddUser();
        var sub = t.AddSubscription(user, t.AddPlan(), SubscriptionStatus.Active, DateTime.UtcNow.AddDays(5), token: "real-token");
        var (ok, _, error) = await svc.CancelAsync(user.Id, sub.Id);
        Assert.False(ok);
        Assert.NotNull(error);
        t.Db.Entry(sub).Reload();
        Assert.Equal(SubscriptionStatus.Active, sub.Status); // still active, so the customer isn't left thinking it stopped
    }

    [Fact]
    public async Task A_demo_subscription_without_a_PayFast_agreement_cancels_locally()
    {
        using var t = new TestDb(); var (svc, api) = Build(t);
        var user = t.AddUser();
        var sub = t.AddSubscription(user, t.AddPlan(), SubscriptionStatus.Active, DateTime.UtcNow.AddDays(5), token: "SIM-TOKEN");
        var (ok, result, _) = await svc.CancelAsync(user.Id, sub.Id);
        Assert.True(ok);
        Assert.False(result!.CancelledWithPayFast);
        Assert.Equal(0, api.Calls);
    }

    [Fact]
    public async Task Nobody_can_cancel_someone_elses_subscription()
    {
        using var t = new TestDb(); var (svc, _) = Build(t);
        var owner = t.AddUser(); var intruder = t.AddUser();
        var sub = t.AddSubscription(owner, t.AddPlan(), SubscriptionStatus.Active, DateTime.UtcNow.AddDays(5));
        Assert.False((await svc.CancelAsync(intruder.Id, sub.Id)).Ok);
    }

    [Fact]
    public async Task Overdue_subscriptions_are_marked_past_due_then_expired()
    {
        using var t = new TestDb(); var (svc, _) = Build(t); var plan = t.AddPlan();
        var late = t.AddSubscription(t.AddUser(), plan, SubscriptionStatus.Active, DateTime.UtcNow.AddDays(-1));
        var dead = t.AddSubscription(t.AddUser(), plan, SubscriptionStatus.PastDue, DateTime.UtcNow.AddDays(-9));
        await svc.SweepExpiredAsync();
        t.Db.Entry(late).Reload(); t.Db.Entry(dead).Reload();
        Assert.Equal(SubscriptionStatus.PastDue, late.Status);
        Assert.Equal(SubscriptionStatus.Expired, dead.Status);
    }
}
