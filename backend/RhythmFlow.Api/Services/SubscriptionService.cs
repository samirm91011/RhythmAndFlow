using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;

namespace RhythmFlow.Api.Services;

public class SubscriptionService(
    AppDbContext db, PayFastService payFast, IPayFastApi payFastApi, NotificationService notifications, ILogger<SubscriptionService> log)
{
    /// <summary>How many days before a charge or an end date the customer is reminded. A year is a bigger surprise than a month.</summary>
    public const int AnnualReminderDays = 14;
    public const int MonthlyReminderDays = 3;
    public const int TrialReminderDays = 2;

    public async Task<CheckoutResponse> CheckoutAsync(Guid userId, int planId, string? billingChoice = null)
    {
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == planId && p.Status == "ACTIVE")
            ?? throw new InvalidOperationException("That plan is not available.");
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        var billing = PlanPricing.NormaliseBilling(billingChoice);
        if (billing == PlanPricing.Annual && !PlanPricing.OffersAnnual(plan))
            throw new InvalidOperationException("That plan is only available monthly.");

        // A second plan while one is still renewing would mean two recurring PayFast agreements, so the customer would be
        // charged twice. To switch, use "Change plan" (it ends the current renewal first) or cancel the current plan.
        if (await db.Subscriptions.AnyAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active))
            throw new InvalidOperationException(
                "You already have an active plan. To switch plans, choose Change plan in My subscription.");

        var sub = await NewPendingAsync(userId, plan, billing, plannedStart: null);
        await db.SaveChangesAsync();
        return new CheckoutResponse(sub.Id, payFast.BuildPaymentUrl(user, plan, sub));
    }

    /// <summary>Only people who have never held a plan get the free trial or the launch price.</summary>
    private async Task<bool> IsNewSubscriberAsync(Guid userId) =>
        !await db.Subscriptions.AnyAsync(s => s.UserId == userId &&
            (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Cancelled ||
             s.Status == SubscriptionStatus.Expired || s.Status == SubscriptionStatus.PastDue));

    private async Task<Subscription> NewPendingAsync(Guid userId, SubscriptionPlan plan, string billing, DateTime? plannedStart)
    {
        // Only one open checkout at a time per user.
        var stale = await db.Subscriptions.Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Pending).ToListAsync();
        foreach (var s in stale) s.Status = SubscriptionStatus.Cancelled;

        var now = DateTime.UtcNow;
        var recurring = PlanPricing.RecurringPrice(plan, billing);
        decimal initial = recurring;
        var trialDays = 0;
        if (plannedStart is not null)
        {
            initial = 0; // the new plan starts when the old one ends; the first real charge is on that day
        }
        else if (await IsNewSubscriberAsync(userId))
        {
            if (plan.TrialDays > 0) { trialDays = plan.TrialDays; initial = 0; }
            else if (billing == PlanPricing.Monthly && PlanPricing.LaunchActive(plan, now)) initial = plan.LaunchPrice!.Value;
        }

        var sub = new Subscription
        {
            UserId = userId, PlanId = plan.Id, Status = SubscriptionStatus.Pending, Billing = billing,
            InitialAmount = initial, TrialDays = trialDays, PlannedStart = plannedStart,
        };
        db.Subscriptions.Add(sub);
        return sub;
    }

    public async Task<List<SubscriptionDto>> MineAsync(Guid userId)
    {
        var subs = await db.Subscriptions.Include(s => s.Plan)
            .Where(s => s.UserId == userId).OrderByDescending(s => s.CreatedAt).ToListAsync();
        return subs.Select(ToDto).ToList();
    }

    public static SubscriptionDto ToDto(Subscription s) => new(
        s.Id, s.PlanId, s.Plan!.Name, s.Plan.Tier, PlanPricing.RecurringPrice(s.Plan, PlanPricing.NormaliseBilling(s.Billing)), s.Status,
        s.StartDate, s.EndDate, EntitlementService.GrantsAccess(s), PlanPricing.NormaliseBilling(s.Billing),
        Renewing: s.Status == SubscriptionStatus.Active && !string.IsNullOrEmpty(s.PayFastReference));

    /// <summary>The person's payments, newest first, each with a receipt number (RF-000123) they can quote to support.</summary>
    public async Task<List<PaymentDto>> PaymentsAsync(Guid userId)
    {
        var rows = await db.Payments.AsNoTracking()
            .Include(p => p.Subscription).ThenInclude(s => s!.Plan)
            .Where(p => p.Subscription!.UserId == userId).ToListAsync();
        return rows.OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id)
            .Select(p => new PaymentDto(p.Id, p.Subscription!.Plan!.Name, p.Amount, p.Status, p.PaymentDate, $"RF-{p.Id:D6}"))
            .ToList();
    }

    /// <summary>
    /// Cancels a subscription. If PayFast holds a recurring agreement for it, PayFast is told first so the customer is not
    /// charged again; if that call fails nothing is cancelled and the customer is asked to try again. Access always runs
    /// to the end of the period already paid for.
    /// </summary>
    public async Task<(bool Ok, CancelResultDto? Result, string? Error)> CancelAsync(Guid userId, int subscriptionId)
    {
        var sub = await db.Subscriptions.Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == subscriptionId && s.UserId == userId);
        if (sub is null || sub.Status != SubscriptionStatus.Active) return (false, null, "No active subscription found.");

        var (ok, error, cancelledWithPayFast) = await StopRenewalAsync(sub);
        if (!ok) return (false, null, error);

        await notifications.AddAsync(userId, "SUBSCRIPTION", "Subscription cancelled",
            $"Your {sub.Plan!.Name} plan won't renew. You keep access until {sub.EndDate:d MMM yyyy}.", "subscription");

        var text = cancelledWithPayFast
            ? $"Your subscription is cancelled and PayFast won't charge you again. You keep access until {sub.EndDate:d MMM yyyy}."
            : $"Your subscription is cancelled. You keep access until {sub.EndDate:d MMM yyyy}.";
        return (true, new CancelResultDto(text, cancelledWithPayFast), null);
    }

    /// <summary>Tells PayFast to stop charging (when it holds an agreement) and marks the subscription cancelled. Nothing changes if PayFast refuses.</summary>
    private async Task<(bool Ok, string? Error, bool WithPayFast)> StopRenewalAsync(Subscription sub)
    {
        var token = sub.PayFastReference;
        var linkedToPayFast = !string.IsNullOrWhiteSpace(token) && !token.StartsWith("SIM-", StringComparison.Ordinal);
        if (linkedToPayFast)
        {
            var (ok, message) = await payFastApi.CancelSubscriptionAsync(token!);
            if (!ok) return (false, message, false);
        }
        sub.Status = SubscriptionStatus.Cancelled;
        await db.SaveChangesAsync();
        return (true, null, linkedToPayFast);
    }

    // ---- Changing plan (upgrade, downgrade, monthly <-> yearly) ----

    /// <summary>
    /// What a change would mean, shown before the customer confirms. The new plan always starts the day the current paid period
    /// ends, so nobody pays twice for the same days and nobody loses days they have paid for.
    /// </summary>
    public async Task<(PlanChangePreviewDto? Preview, string? Error)> PreviewChangeAsync(Guid userId, int subscriptionId, int newPlanId, string? billingChoice)
    {
        var (sub, newPlan, billing, error) = await LoadChangeAsync(userId, subscriptionId, newPlanId, billingChoice);
        if (error is not null) return (null, error);

        var curBilling = PlanPricing.NormaliseBilling(sub!.Billing);
        var curPrice = PlanPricing.RecurringPrice(sub.Plan!, curBilling);
        var newPrice = PlanPricing.RecurringPrice(newPlan!, billing);
        var diff = PerMonth(newPrice, billing) - PerMonth(curPrice, curBilling);
        var effective = sub.EndDate!.Value;

        string impact;
        if (newPlan!.Tier > sub.Plan!.Tier)
            impact = $"You keep {sub.Plan.Name} until {effective:d MMM yyyy}. From then you move up to {newPlan.Name} and get everything it includes, including its extra programmes.";
        else if (newPlan.Tier < sub.Plan.Tier)
            impact = $"You keep {sub.Plan.Name} until {effective:d MMM yyyy}. From then you move to {newPlan.Name}, so programmes that need the higher plan will be locked.";
        else
            impact = $"You keep {sub.Plan.Name} until {effective:d MMM yyyy}. From then you are billed {(billing == PlanPricing.Annual ? "yearly" : "monthly")} for {newPlan.Name}. Your access stays the same.";

        return (new PlanChangePreviewDto(sub.Plan.Name, curPrice, curBilling, newPlan.Name, newPrice, billing,
            Math.Round(diff, 2), effective, impact), null);
    }

    /// <summary>Confirms a plan change: the current renewal is stopped and the new plan is set up to start when the current period ends.</summary>
    public async Task<(CheckoutResponse? Result, string? Error)> ChangePlanAsync(Guid userId, int subscriptionId, int newPlanId, string? billingChoice)
    {
        var (sub, newPlan, billing, error) = await LoadChangeAsync(userId, subscriptionId, newPlanId, billingChoice);
        if (error is not null) return (null, error);

        var (ok, stopError, _) = await StopRenewalAsync(sub!);
        if (!ok) return (null, stopError);

        var user = await db.Users.FirstAsync(u => u.Id == userId);
        var pending = await NewPendingAsync(userId, newPlan!, billing, plannedStart: sub!.EndDate);
        await db.SaveChangesAsync();
        await notifications.AddAsync(userId, "SUBSCRIPTION", "Plan change started",
            $"Finish setting up {newPlan!.Name} on the PayFast page. It starts on {sub.EndDate:d MMM yyyy}; until then you keep {sub.Plan!.Name}.", "subscription");
        return (new CheckoutResponse(pending.Id, payFast.BuildPaymentUrl(user, newPlan, pending)), null);
    }

    private async Task<(Subscription? Sub, SubscriptionPlan? NewPlan, string Billing, string? Error)> LoadChangeAsync(
        Guid userId, int subscriptionId, int newPlanId, string? billingChoice)
    {
        var billing = PlanPricing.NormaliseBilling(billingChoice);
        var sub = await db.Subscriptions.Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.UserId == userId);
        if (sub is null || sub.Status != SubscriptionStatus.Active || sub.EndDate is null)
            return (null, null, billing, "You need an active plan to change it.");
        var newPlan = await db.Plans.FirstOrDefaultAsync(p => p.Id == newPlanId && p.Status == "ACTIVE");
        if (newPlan is null) return (null, null, billing, "That plan is not available.");
        if (billing == PlanPricing.Annual && !PlanPricing.OffersAnnual(newPlan))
            return (null, null, billing, "That plan is only available monthly.");
        if (newPlan.Id == sub.PlanId && billing == PlanPricing.NormaliseBilling(sub.Billing))
            return (null, null, billing, "That is the plan you already have.");
        if (sub.EndDate.Value <= DateTime.UtcNow.AddDays(1))
            return (null, null, billing, "Your current period ends very soon, so please choose your new plan once it has ended.");
        return (sub, newPlan, billing, null);
    }

    private static string Rand(decimal amount) => amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    private static decimal PerMonth(decimal price, string billing) => billing == PlanPricing.Annual ? price / 12m : price;

    // ---- Payment notifications from PayFast ----

    /// <summary>
    /// Applies a payment result. Called from the PayFast ITN handler (BR-04: a subscription only becomes
    /// active once the payment is confirmed server-to-server) and from the dev simulator.
    /// </summary>
    public async Task<bool> ApplyPaymentResultAsync(int subscriptionId, string paymentStatus, decimal amountGross, string? pfPaymentId, string? token)
    {
        var sub = await db.Subscriptions.Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == subscriptionId);
        if (sub is null || sub.Plan is null) return false;

        // Idempotency: PayFast can retry the same notification.
        if (pfPaymentId is not null && await db.Payments.AnyAsync(p => p.TransactionReference == pfPaymentId))
            return true;

        var billing = PlanPricing.NormaliseBilling(sub.Billing);
        switch (paymentStatus.ToUpperInvariant())
        {
            case "COMPLETE":
                var recurring = PlanPricing.RecurringPrice(sub.Plan, billing);
                var firstPayment = sub.Status == SubscriptionStatus.Pending;
                // A free start (trial or a plan that begins later) is confirmed with a 0.00 notice; it is not money, so it is not a payment record.
                if (amountGross == 0)
                {
                    if (!firstPayment || (sub.InitialAmount ?? recurring) != 0) return !firstPayment; // a repeat of an earlier notice is harmless
                    ActivateFreeStart(sub, token);
                    await db.SaveChangesAsync();
                    await notifications.AddAsync(sub.UserId, "SUBSCRIPTION", sub.TrialDays > 0 ? "Your free trial has started" : "Your new plan is set up",
                        sub.TrialDays > 0
                            ? $"Enjoy {sub.Plan.Name} free until {sub.EndDate:d MMM yyyy}. After that {Rand(recurring)} is charged each {PlanPricing.PeriodWord(billing)} unless you cancel."
                            : $"{sub.Plan.Name} starts on {sub.StartDate:d MMM yyyy} and then renews each {PlanPricing.PeriodWord(billing)}.", "subscription");
                    return true;
                }
                var expected = firstPayment ? sub.InitialAmount ?? recurring : recurring;
                if (amountGross != expected)
                {
                    log.LogWarning("ITN amount {Amount} does not match the expected {Expected} for subscription {Id}", amountGross, expected, sub.Id);
                    return false; // amount must match what the plan says
                }
                db.Payments.Add(new Payment
                {
                    SubscriptionId = sub.Id, Amount = amountGross, Status = "COMPLETE", TransactionReference = pfPaymentId,
                });
                var now = DateTime.UtcNow;
                if (firstPayment)
                {
                    sub.StartDate ??= now;
                    sub.EndDate = PlanPricing.AddPeriod(now, billing);
                }
                else if (sub.AwaitingFirstCharge)
                {
                    // The first real charge after a trial or a plan change: the paid period starts now (or on the planned day if that is later).
                    var begin = sub.StartDate is { } s0 && s0 > now ? s0 : now;
                    sub.EndDate = PlanPricing.AddPeriod(begin, billing);
                    sub.AwaitingFirstCharge = false;
                }
                else
                {
                    // Each recurring payment extends the paid period by one month or one year.
                    var from = sub.EndDate is { } end && end > now ? end : now;
                    sub.EndDate = PlanPricing.AddPeriod(from, billing);
                }
                sub.Status = SubscriptionStatus.Active;
                if (!string.IsNullOrEmpty(token)) sub.PayFastReference = token;
                await db.SaveChangesAsync();
                await notifications.AddAsync(sub.UserId, "PAYMENT",
                    firstPayment ? "You're subscribed!" : "Payment received",
                    firstPayment ? $"Your {sub.Plan.Name} plan is active. Enjoy your practice."
                                 : $"Thanks! Your {sub.Plan.Name} plan renews until {sub.EndDate:d MMM yyyy}.", "subscription");
                return true;
            case "FAILED":
                db.Payments.Add(new Payment { SubscriptionId = sub.Id, Amount = amountGross, Status = "FAILED", TransactionReference = pfPaymentId });
                sub.Status = sub.Status == SubscriptionStatus.Active ? SubscriptionStatus.PastDue : SubscriptionStatus.Failed;
                await db.SaveChangesAsync();
                await notifications.AddAsync(sub.UserId, "PAYMENT", "Payment didn't go through",
                    "We couldn't take your subscription payment. Please check your payment method with PayFast.", "plans");
                return true;
            case "CANCELLED":
                // Pending checkout abandoned, or the customer cancelled the recurring agreement from PayFast itself.
                if (sub.Status == SubscriptionStatus.Pending) sub.Status = SubscriptionStatus.Cancelled;
                else if (sub.Status == SubscriptionStatus.Active)
                {
                    sub.Status = SubscriptionStatus.Cancelled;
                    await notifications.AddAsync(sub.UserId, "SUBSCRIPTION", "Subscription cancelled",
                        $"Your plan won't renew. You keep access until {sub.EndDate:d MMM yyyy}.", "subscription");
                }
                await db.SaveChangesAsync();
                return true;
            default:
                return true; // PENDING etc.: nothing to do yet
        }
    }

    /// <summary>
    /// A trial starts today and runs for its free days. A plan change starts on its planned day, with one day of room so the
    /// first charge (taken on that day) can arrive before access would lapse.
    /// </summary>
    private static void ActivateFreeStart(Subscription sub, string? token)
    {
        var now = DateTime.UtcNow;
        sub.Status = SubscriptionStatus.Active;
        sub.AwaitingFirstCharge = true;
        if (sub.TrialDays > 0)
        {
            sub.StartDate = now;
            sub.EndDate = now.AddDays(sub.TrialDays);
        }
        else
        {
            var start = sub.PlannedStart ?? now;
            sub.StartDate = start;
            sub.EndDate = start.AddDays(1);
        }
        if (!string.IsNullOrEmpty(token)) sub.PayFastReference = token;
    }

    /// <summary>
    /// Marks overdue subscriptions (PAST_DUE after the period ends, EXPIRED a week later: the client has not set a grace period yet)
    /// and sends the renewal and expiry reminders.
    /// </summary>
    public async Task SweepExpiredAsync()
    {
        var now = DateTime.UtcNow;
        var due = await db.Subscriptions.Where(s => s.Status == SubscriptionStatus.Active && s.EndDate < now && !s.AwaitingFirstCharge).ToListAsync();
        foreach (var s in due)
        {
            s.Status = SubscriptionStatus.PastDue;
            await notifications.AddAsync(s.UserId, "PAYMENT", "Your subscription is overdue",
                "We haven't received your latest payment. Please update it with PayFast to keep your access.", "plans");
        }
        var dead = await db.Subscriptions.Where(s => s.Status == SubscriptionStatus.PastDue && s.EndDate < now.AddDays(-7)).ToListAsync();
        foreach (var s in dead)
        {
            s.Status = SubscriptionStatus.Expired;
            await notifications.AddAsync(s.UserId, "SUBSCRIPTION", "Your subscription has ended",
                "Choose a plan any time to pick up where you left off.", "plans");
        }
        if (due.Count + dead.Count > 0) await db.SaveChangesAsync();
        await SendRemindersAsync(now);
    }

    /// <summary>One reminder per renewal: a few days before money is taken (a fortnight for a yearly plan) or before access ends.</summary>
    public async Task SendRemindersAsync(DateTime now)
    {
        var horizon = now.AddDays(AnnualReminderDays);
        var upcoming = await db.Subscriptions.Include(s => s.Plan)
            .Where(s => (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Cancelled) &&
                        s.EndDate != null && s.EndDate > now && s.EndDate <= horizon).ToListAsync();
        var sent = 0;
        foreach (var s in upcoming)
        {
            if (s.ReminderForEnd == s.EndDate) continue;
            var billing = PlanPricing.NormaliseBilling(s.Billing);
            var days = s.AwaitingFirstCharge && s.TrialDays > 0 ? TrialReminderDays
                : billing == PlanPricing.Annual ? AnnualReminderDays : MonthlyReminderDays;
            if (s.EndDate > now.AddDays(days)) continue;

            var price = PlanPricing.RecurringPrice(s.Plan!, billing);
            string title, body;
            if (s.Status == SubscriptionStatus.Cancelled)
            {
                // A plan change cancels the old plan on purpose; its replacement starts the day it ends, so no warning is needed.
                var replaced = await db.Subscriptions.AnyAsync(o => o.UserId == s.UserId && o.Id != s.Id && o.PlannedStart != null &&
                    (o.Status == SubscriptionStatus.Pending || o.Status == SubscriptionStatus.Active));
                if (replaced) { s.ReminderForEnd = s.EndDate; sent++; continue; }
                title = "Your access ends soon";
                body = $"Your {s.Plan!.Name} access ends on {s.EndDate:d MMM yyyy}. Choose a plan to keep going.";
            }
            else if (s.AwaitingFirstCharge && s.TrialDays > 0)
            {
                title = "Your free trial ends soon";
                body = $"Your free trial ends on {s.EndDate:d MMM yyyy}, then R{Rand(price)} is charged. Cancel in My subscription before then if you don't want to continue.";
            }
            else
            {
                title = billing == PlanPricing.Annual ? "Your yearly plan renews soon" : "Your plan renews soon";
                body = $"Your {s.Plan!.Name} plan renews on {s.EndDate:d MMM yyyy} for R{Rand(price)}. Cancel in My subscription before then if you don't want it to continue.";
            }
            await notifications.AddAsync(s.UserId, "SUBSCRIPTION", title, body, "subscription");
            s.ReminderForEnd = s.EndDate;
            sent++;
        }
        if (sent > 0) await db.SaveChangesAsync();
    }
}
