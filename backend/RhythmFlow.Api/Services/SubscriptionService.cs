using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;

namespace RhythmFlow.Api.Services;

public class SubscriptionService(
    AppDbContext db, PayFastService payFast, IPayFastApi payFastApi, NotificationService notifications, ILogger<SubscriptionService> log)
{
    public async Task<CheckoutResponse> CheckoutAsync(Guid userId, int planId)
    {
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == planId && p.Status == "ACTIVE")
            ?? throw new InvalidOperationException("That plan is not available.");
        var user = await db.Users.FirstAsync(u => u.Id == userId);

        // Only one open checkout at a time per user.
        var stale = await db.Subscriptions.Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Pending).ToListAsync();
        foreach (var s in stale) s.Status = SubscriptionStatus.Cancelled;

        var sub = new Subscription { UserId = userId, PlanId = plan.Id, Status = SubscriptionStatus.Pending };
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        return new CheckoutResponse(sub.Id, payFast.BuildPaymentUrl(user, plan, sub));
    }

    public async Task<List<SubscriptionDto>> MineAsync(Guid userId)
    {
        var subs = await db.Subscriptions.Include(s => s.Plan)
            .Where(s => s.UserId == userId).OrderByDescending(s => s.CreatedAt).ToListAsync();
        return subs.Select(s => new SubscriptionDto(s.Id, s.PlanId, s.Plan!.Name, s.Plan.Tier, s.Plan.Price, s.Status,
            s.StartDate, s.EndDate, EntitlementService.GrantsAccess(s))).ToList();
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

        var token = sub.PayFastReference;
        var linkedToPayFast = !string.IsNullOrWhiteSpace(token) && !token.StartsWith("SIM-", StringComparison.Ordinal);
        var cancelledWithPayFast = false;
        if (linkedToPayFast)
        {
            var (ok, message) = await payFastApi.CancelSubscriptionAsync(token!);
            if (!ok) return (false, null, message);
            cancelledWithPayFast = true;
        }

        sub.Status = SubscriptionStatus.Cancelled;
        await db.SaveChangesAsync();
        await notifications.AddAsync(userId, "SUBSCRIPTION", "Subscription cancelled",
            $"Your {sub.Plan!.Name} plan won't renew. You keep access until {sub.EndDate:d MMM yyyy}.", "subscription");

        var text = cancelledWithPayFast
            ? $"Your subscription is cancelled and PayFast won't charge you again. You keep access until {sub.EndDate:d MMM yyyy}."
            : $"Your subscription is cancelled. You keep access until {sub.EndDate:d MMM yyyy}.";
        return (true, new CancelResultDto(text, cancelledWithPayFast), null);
    }

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

        switch (paymentStatus.ToUpperInvariant())
        {
            case "COMPLETE":
                if (amountGross != sub.Plan.Price)
                {
                    log.LogWarning("ITN amount {Amount} does not match plan price {Price} for subscription {Id}", amountGross, sub.Plan.Price, sub.Id);
                    return false; // amount must match the plan price
                }
                var firstPayment = sub.Status == SubscriptionStatus.Pending;
                db.Payments.Add(new Payment
                {
                    SubscriptionId = sub.Id, Amount = amountGross, Status = "COMPLETE", TransactionReference = pfPaymentId,
                });
                var now = DateTime.UtcNow;
                // First payment starts the period; each recurring payment extends it by a month.
                var from = sub.EndDate is { } end && end > now && !firstPayment ? end : now;
                sub.StartDate ??= now;
                sub.EndDate = from.AddMonths(1);
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

    /// <summary>Marks overdue subscriptions. PAST_DUE after the period ends, EXPIRED a week later (no grace period is specified by the client yet).</summary>
    public async Task SweepExpiredAsync()
    {
        var now = DateTime.UtcNow;
        var due = await db.Subscriptions.Where(s => s.Status == SubscriptionStatus.Active && s.EndDate < now).ToListAsync();
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
    }
}
