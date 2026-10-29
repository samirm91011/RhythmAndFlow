using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

/// <summary>The emails sent after a PayFast payment: a receipt, trial and plan-start notes, cancellation, refund, and the copy to the studio.</summary>
public class PaymentEmailTests
{
    private const string Studio = "info@rhythmandflow.co.za";

    private static (SubscriptionService svc, FakeEmail mail) Build(TestDb t, IEmailSender? sender = null)
    {
        var mail = new FakeEmail();
        var payFast = new PayFastService(Options.Create(new PayFastOptions { MerchantId = "1", MerchantKey = "k", Passphrase = "p" }));
        var svc = new SubscriptionService(t.Db, payFast, new FakePayFastApi(), new NotificationService(t.Db), NullLogger<SubscriptionService>.Instance,
            sender ?? mail, Options.Create(new EventOptions()));
        return (svc, mail);
    }

    [Fact]
    public async Task A_first_payment_sends_the_customer_a_receipt_and_the_studio_a_copy()
    {
        using var t = new TestDb(); var (svc, mail) = Build(t);
        var user = t.AddUser("Samir Mahabeer"); var plan = t.AddPlan(tier: 1, price: 149); plan.Name = "Move & Breathe"; t.Db.SaveChanges();
        var sub = t.AddSubscription(user, plan, SubscriptionStatus.Pending);

        await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 149m, "PF-777", "TOKEN");

        var receipt = Assert.Single(mail.Sent, m => m.To == user.Email);
        Assert.Equal("Payment received: R149.00 for Move & Breathe", receipt.Subject);
        Assert.Contains("Hi Samir,", receipt.Body);
        Assert.Contains("welcome to Rhythm & Flow", receipt.Body);
        Assert.Contains("Receipt number: RF-S", receipt.Body);
        Assert.Contains("Amount paid: R149.00 (ZAR)", receipt.Body);
        Assert.Contains("PayFast payment reference: PF-777", receipt.Body);
        Assert.Contains("South African time", receipt.Body);
        Assert.Contains("renews each month unless you cancel", receipt.Body);
        Assert.Contains(Studio, receipt.Body);
        Assert.DoesNotContain("VAT", receipt.Body);

        var copy = Assert.Single(mail.Sent, m => m.To == Studio);
        Assert.StartsWith("New subscriber payment: Samir Mahabeer", copy.Subject);
        Assert.Contains("Amount: R149.00", copy.Body); Assert.Contains("PayFast reference: PF-777", copy.Body);
    }

    [Fact]
    public async Task A_renewal_is_a_receipt_without_the_welcome_and_the_receipt_numbers_differ()
    {
        using var t = new TestDb(); var (svc, mail) = Build(t);
        var user = t.AddUser(); var plan = t.AddPlan(price: 99);
        var sub = t.AddSubscription(user, plan, SubscriptionStatus.Pending);
        await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 99m, "PF-1", "T");
        await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 99m, "PF-2", "T");

        var receipts = mail.Sent.Where(m => m.To == user.Email).ToList();
        Assert.Equal(2, receipts.Count);
        Assert.Contains("welcome", receipts[0].Body); Assert.DoesNotContain("welcome", receipts[1].Body);
        Assert.StartsWith("New subscriber payment", mail.Sent.First(m => m.To == Studio).Subject);
        Assert.StartsWith("Renewal payment", mail.Sent.Last(m => m.To == Studio).Subject);
        Assert.NotEqual(receipts[0].Body.Split("Receipt number: ")[1][..10], receipts[1].Body.Split("Receipt number: ")[1][..10]);
    }

    [Fact]
    public async Task PayFast_repeating_the_same_notice_does_not_email_twice()
    {
        using var t = new TestDb(); var (svc, mail) = Build(t);
        var user = t.AddUser(); var sub = t.AddSubscription(user, t.AddPlan(price: 99), SubscriptionStatus.Pending);
        await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 99m, "PF-1", null);
        var after = mail.Sent.Count;
        await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 99m, "PF-1", null);
        Assert.Equal(after, mail.Sent.Count);
    }

    [Fact]
    public async Task A_refused_payment_for_the_wrong_amount_sends_no_receipt()
    {
        using var t = new TestDb(); var (svc, mail) = Build(t);
        var sub = t.AddSubscription(t.AddUser(), t.AddPlan(price: 199), SubscriptionStatus.Pending);
        await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 1m, "PF-1", null);
        Assert.Empty(mail.Sent);
    }

    [Fact]
    public async Task A_failed_payment_warns_the_customer_and_tells_the_studio()
    {
        using var t = new TestDb(); var (svc, mail) = Build(t);
        var user = t.AddUser(); var sub = t.AddSubscription(user, t.AddPlan(price: 99), SubscriptionStatus.Active, DateTime.UtcNow.AddDays(10));
        await svc.ApplyPaymentResultAsync(sub.Id, "FAILED", 99m, "PF-F", null);
        Assert.Contains(mail.Sent, m => m.To == user.Email && m.Subject.Contains("didn't go through"));
        Assert.Contains(mail.Sent, m => m.To == Studio && m.Subject.StartsWith("Payment failed"));
    }

    [Fact]
    public async Task Cancelling_emails_the_customer_and_the_studio_and_says_until_when_access_lasts()
    {
        using var t = new TestDb(); var (svc, mail) = Build(t);
        var user = t.AddUser(); var sub = t.AddSubscription(user, t.AddPlan(price: 99), SubscriptionStatus.Active, DateTime.UtcNow.AddDays(20), "TOKEN");

        var (ok, _, _) = await svc.CancelAsync(user.Id, sub.Id);

        Assert.True(ok);
        var m1 = Assert.Single(mail.Sent, m => m.To == user.Email);
        Assert.Equal("Your plan won't renew", m1.Subject); Assert.Contains("You keep your access until", m1.Body);
        Assert.Contains(mail.Sent, m => m.To == Studio && m.Subject.StartsWith("Plan cancelled"));
    }

    [Fact]
    public async Task A_free_start_says_no_money_was_taken_and_when_the_first_charge_is()
    {
        using var t = new TestDb(); var (svc, mail) = Build(t);
        var user = t.AddUser(); var plan = t.AddPlan(tier: 2, price: 249); plan.TrialDays = 7; t.Db.SaveChanges();
        var sub = t.AddSubscription(user, plan, SubscriptionStatus.Pending); sub.TrialDays = 7; sub.InitialAmount = 0; t.Db.SaveChanges();

        await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 0m, null, "TOKEN");

        var m1 = Assert.Single(mail.Sent, m => m.To == user.Email);
        Assert.Equal("Your free trial has started", m1.Subject);
        Assert.Contains("No payment has been taken", m1.Body); Assert.Contains("R249.00", m1.Body);
        Assert.Contains(mail.Sent, m => m.To == Studio && m.Subject.StartsWith("Free trial started"));
    }

    private sealed class FailingEmail : IEmailSender
    {
        public Task<bool> SendAsync(string to, string subject, string body) => throw new InvalidOperationException("mail server down");
    }

    [Fact]
    public async Task A_broken_mail_server_never_undoes_a_payment()
    {
        using var t = new TestDb(); var (svc, _) = Build(t, new FailingEmail());
        var sub = t.AddSubscription(t.AddUser(), t.AddPlan(price: 99), SubscriptionStatus.Pending);

        Assert.True(await svc.ApplyPaymentResultAsync(sub.Id, "COMPLETE", 99m, "PF-1", null));

        t.Db.Entry(sub).Reload();
        Assert.Equal(SubscriptionStatus.Active, sub.Status); Assert.Single(t.Db.Payments);
    }

    [Fact]
    public void The_refund_email_names_the_amount_and_the_wording_is_plain()
    {
        var user = new User { FullName = "Jo Smith", Email = "jo@example.com" };
        var (subject, body) = PaymentEmails.RefundSent(user, "Breathwork Workshop", 250m, new EventOptions());
        Assert.Equal("Refund sent: R250.00", subject);
        Assert.Contains("We've refunded R250.00 for Breathwork Workshop", body);
        Assert.Contains("Hi Jo,", body);
    }
}
