using System.Globalization;
using System.Text;
using RhythmFlow.Api.Domain;

namespace RhythmFlow.Api.Services;

/// <summary>
/// The emails sent around a PayFast payment: a receipt for every payment taken, the start of a free trial or a new plan, a cancellation,
/// a refund, and a short note to the studio so the client sees each payment as it happens. Times are South African time.
/// Rhythm &amp; Flow is not VAT registered, so these are receipts, not tax invoices.
/// </summary>
public static class PaymentEmails
{
    public static string Reference(int paymentId) => $"RF-S{paymentId:D6}";

    private static string First(User u) => u.FullName.Split(' ')[0];
    private static string Rand(decimal amount) => EventEmails.Rand(amount);
    private static string When(DateTime utc) => EventEmails.Sast(utc).ToString("dddd d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture) + " (South African time)";
    private static string Day(DateTime? utc) => SubscriptionService.Day(utc);
    private static string Period(string billing) => PlanPricing.PeriodWord(billing);
    private static string Billed(string billing) => billing == PlanPricing.Annual ? "yearly" : "monthly";

    private static string Footer(EventOptions o) =>
        $"\nQuestions? Email {o.ContactEmail} or WhatsApp {o.ContactWhatsApp}.\n\n{EventEmails.SignOff}\n\nRhythm & Flow";

    private const string ManagePlan = "You can change or cancel your plan at any time in the app: You, then Subscription. Cancelling stops future payments and you keep your access until the end of the period you have already paid for.";

    /// <summary>The receipt for a payment PayFast has confirmed.</summary>
    public static (string Subject, string Body) Receipt(User user, SubscriptionPlan plan, string billing, decimal amount, DateTime paidAt,
        int paymentId, string? pfReference, bool firstPayment, DateTime? endDate, EventOptions o)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Hi {First(user)},").AppendLine();
        sb.AppendLine(firstPayment
            ? $"Thank you, and welcome to Rhythm & Flow. Your payment has been received and your {plan.Name} plan is now active."
            : $"Thank you. We've received your {Billed(billing)} payment for {plan.Name}.").AppendLine();
        sb.AppendLine("Receipt");
        sb.AppendLine($"Receipt number: {Reference(paymentId)}");
        sb.AppendLine($"Date and time: {When(paidAt)}");
        sb.AppendLine($"Plan: {plan.Name} ({Billed(billing)})");
        sb.AppendLine($"Amount paid: {Rand(amount)} (ZAR)");
        sb.AppendLine("Paid with: PayFast");
        if (!string.IsNullOrWhiteSpace(pfReference)) sb.AppendLine($"PayFast payment reference: {pfReference}");
        if (endDate is not null) sb.AppendLine($"Your plan runs until: {Day(endDate)}, and renews each {Period(billing)} unless you cancel.");
        sb.AppendLine().AppendLine(ManagePlan);
        sb.Append(Footer(o));
        return ($"Payment received: {Rand(amount)} for {plan.Name}", sb.ToString());
    }

    /// <summary>A free trial started, or a new plan was set up to begin later: no money has been taken yet.</summary>
    public static (string Subject, string Body) FreeStart(User user, SubscriptionPlan plan, string billing, int trialDays, decimal recurring,
        DateTime? startDate, DateTime? endDate, EventOptions o)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Hi {First(user)},").AppendLine();
        if (trialDays > 0)
        {
            sb.AppendLine($"Your free trial of {plan.Name} has started. Enjoy every practice in it until {Day(endDate)}.").AppendLine();
            sb.AppendLine($"No payment has been taken. After the trial, {Rand(recurring)} is charged each {Period(billing)} on PayFast unless you cancel before {Day(endDate)}.");
        }
        else
        {
            sb.AppendLine($"Your new plan, {plan.Name}, is set up. It starts on {Day(startDate)}, and until then you keep your current plan.").AppendLine();
            sb.AppendLine($"No payment has been taken today. From {Day(startDate)}, {Rand(recurring)} is charged each {Period(billing)} on PayFast.");
        }
        sb.AppendLine().AppendLine(ManagePlan);
        sb.Append(Footer(o));
        return (trialDays > 0 ? "Your free trial has started" : "Your new plan is set up", sb.ToString());
    }

    public static (string Subject, string Body) Cancelled(User user, SubscriptionPlan plan, DateTime? endDate, EventOptions o)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Hi {First(user)},").AppendLine();
        sb.AppendLine($"Your {plan.Name} plan won't renew, and you won't be charged again.");
        if (endDate is not null && endDate > DateTime.UtcNow) sb.AppendLine($"You keep your access until {Day(endDate)}.");
        sb.AppendLine().AppendLine("If you change your mind, you can choose a plan again at any time in the app.");
        sb.Append(Footer(o));
        return ("Your plan won't renew", sb.ToString());
    }

    public static (string Subject, string Body) RefundSent(User user, string what, decimal amount, EventOptions o)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Hi {First(user)},").AppendLine();
        sb.AppendLine($"We've refunded {Rand(amount)} for {what}. It can take a few days to show on your statement.");
        sb.Append(Footer(o));
        return ($"Refund sent: {Rand(amount)}", sb.ToString());
    }

    /// <summary>The short note to the studio's own inbox, so every payment is seen without opening PayFast.</summary>
    public static (string Subject, string Body) StudioCopy(string headline, User user, IEnumerable<string> details)
    {
        var sb = new StringBuilder();
        sb.AppendLine(headline).AppendLine();
        sb.AppendLine($"Customer: {user.FullName} ({user.Email})");
        foreach (var d in details) sb.AppendLine(d);
        sb.AppendLine().AppendLine("This is an automatic note from the Rhythm & Flow app.");
        return ($"{headline}: {user.FullName}", sb.ToString());
    }
}
