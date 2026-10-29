using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;

namespace RhythmFlow.Api.Services;

/// <summary>
/// Paid events: tickets, the waiting list, account credit, cancellations and refunds, and the emails and reminders around them.
/// Free classes keep using <see cref="BookingService"/>. Money rules come from the client's feedback of 20 Oct 2026.
/// </summary>
public class EventBookingService(
    AppDbContext db, PayFastService payFast, NotificationService notifications, IEmailSender email, IOptions<EventOptions> options)
{
    private readonly EventOptions _o = options.Value;

    // ---- Can this person buy a ticket now? ----

    public static string? BuyBlockedReason(ClassSession c, int tier, DateTime now, int taken)
    {
        if (c.Status != "SCHEDULED") return "This event has been cancelled.";
        if (c.StartTime <= now) return "This event has already started.";
        if (now >= c.StartTime.AddHours(-c.SalesCloseHours)) return "Ticket sales for this event have closed.";
        if (c.SalesOpenAt is { } open)
        {
            var early = tier >= 2 ? c.MemberEarlyHours : 0;
            if (now < open.AddHours(-early))
                return $"Ticket sales open on {EventEmails.Sast(open.AddHours(-early)):d MMM} at {EventEmails.Sast(open.AddHours(-early)):HH:mm}.";
        }
        if (taken >= c.Capacity) return "This event is sold out.";
        return null;
    }

    /// <summary>Places that are taken or held right now: confirmed tickets, tickets waiting for payment, and spots offered from the waiting list.</summary>
    public async Task<int> TakenAsync(int classId, DateTime now, int? exceptBookingId = null) =>
        await db.Bookings.CountAsync(b => b.ClassId == classId && b.Id != exceptBookingId &&
            (b.Status == BookingStatus.Booked ||
             ((b.Status == BookingStatus.PendingPayment || b.Status == BookingStatus.Waitlisted) && b.HoldUntil > now)));

    private async Task<int> TierAsync(Guid userId) => await new EntitlementService(db).GetUserTierAsync(userId);

    private static decimal Total(Booking b) => b.AmountPaid + b.CreditApplied;

    // ---- Quote and ticket ----

    public async Task<TicketQuoteDto> QuoteAsync(Guid userId, int classId)
    {
        var c = await db.Classes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == classId);
        if (c is null) return new(0, 0, 0, 0, false, "That event is not available.");
        var now = DateTime.UtcNow;
        var credit = await CreditBalanceAsync(userId, now);
        var applied = Math.Min(credit, c.Price);
        var mine = await db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.UserId == userId && b.ClassId == classId &&
            (b.Status == BookingStatus.PendingPayment || b.Status == BookingStatus.Waitlisted));
        var reason = BuyBlockedReason(c, await TierAsync(userId), now, await TakenAsync(classId, now, mine?.Id));
        if (await db.Bookings.AnyAsync(b => b.UserId == userId && b.ClassId == classId && b.Status == BookingStatus.Booked))
            reason = "You already have a ticket for this event.";
        return new(c.Price, credit, applied, c.Price - applied, reason is null, reason);
    }

    /// <summary>Starts (or finishes) buying a ticket. Returns a PayFast link, or a confirmed booking if account credit covered it all.</summary>
    public async Task<(TicketResponse? Result, string? Error)> TicketAsync(Guid userId, int classId, bool useCredit)
    {
        var c = await db.Classes.FirstOrDefaultAsync(x => x.Id == classId);
        if (c is null) return (null, "That event is not available.");
        if (c.Price <= 0) return (null, "This event is free. Book it directly.");
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        var now = DateTime.UtcNow;

        if (await db.Bookings.AnyAsync(b => b.UserId == userId && b.ClassId == classId && b.Status == BookingStatus.Booked))
            return (null, "You already have a ticket for this event.");

        // Continue an unfinished purchase, or use a spot offered from the waiting list.
        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.UserId == userId && b.ClassId == classId &&
            (b.Status == BookingStatus.PendingPayment || b.Status == BookingStatus.Waitlisted));
        var offered = booking is { Status: BookingStatus.Waitlisted } && booking.HoldUntil > now;
        if (booking is { Status: BookingStatus.Waitlisted } && !offered)
            return (null, "You're on the waiting list. We'll let you know if a spot opens.");

        var taken = await TakenAsync(classId, now, booking?.Id);
        var blocked = BuyBlockedReason(c, await TierAsync(userId), now, offered ? 0 : taken);
        if (blocked is not null)
            return (null, blocked == "This event is sold out." && c.WaitlistEnabled ? "This event is sold out. You can join the waiting list." : blocked);

        await using var tx = await db.Database.BeginTransactionAsync();
        if (booking is null)
        {
            booking = new Booking { UserId = userId, ClassId = classId, Status = BookingStatus.PendingPayment };
            db.Bookings.Add(booking);
        }
        else await ReleaseCreditAsync(booking);   // start from the full price again

        var applied = 0m; string? uses = null;
        if (useCredit) (applied, uses) = await ConsumeCreditAsync(userId, c.Price, now);
        booking.CreditApplied = applied; booking.CreditUses = uses; booking.AmountDue = c.Price - applied;
        booking.Status = BookingStatus.PendingPayment;
        booking.HoldUntil = now.AddMinutes(_o.HoldMinutes);
        booking.BookingDate = now;
        await db.SaveChangesAsync();

        if (booking.AmountDue == 0)
        {
            await ConfirmAsync(booking, c, user, 0, null);
            await tx.CommitAsync();
            return (new TicketResponse(booking.Id, EventEmails.Reference(booking.Id), true, null, 0, applied,
                "You're booked. We've emailed your confirmation."), null);
        }
        await tx.CommitAsync();
        return (new TicketResponse(booking.Id, EventEmails.Reference(booking.Id), false, payFast.BuildEventPaymentUrl(user, c, booking),
            booking.AmountDue, applied, $"Your ticket is held for {_o.HoldMinutes} minutes while you pay."), null);
    }

    /// <summary>Marks the ticket confirmed and sends the confirmation email, the separate joining-link email, and an in-app notice.</summary>
    private async Task ConfirmAsync(Booking b, ClassSession c, User user, decimal cashPaid, string? pfPaymentId)
    {
        b.Status = BookingStatus.Booked;
        b.AmountPaid = cashPaid;
        b.PayFastPaymentId = pfPaymentId;
        b.HoldUntil = null;
        await db.SaveChangesAsync();

        var (subject, body) = EventEmails.Confirmation(user, c, b, _o);
        await email.SendAsync(user.Email, subject, body);
        await SendJoinLinkAsync(b, c, user);
        try
        {
            var copy = PaymentEmails.StudioCopy(cashPaid > 0 ? "New ticket payment" : "New booking", user,
                [$"Event: {EventEmails.Describe(c)}", $"Reference: {EventEmails.Reference(b.Id)}", $"Paid on PayFast: {EventEmails.Rand(cashPaid)}", $"PayFast reference: {pfPaymentId ?? "none"}"]);
            await email.SendAsync(_o.ContactEmail, copy.Subject, copy.Body);
        }
        catch { /* a failed copy to the studio never undoes a booking */ }
        await notifications.AddAsync(user.Id, "BOOKING", "Booking confirmed",
            $"You're booked for {EventEmails.Describe(c)}. Reference {EventEmails.Reference(b.Id)}.", "bookings");
    }

    private async Task SendJoinLinkAsync(Booking b, ClassSession c, User user)
    {
        if (c.Format != EventKinds.Online || string.IsNullOrWhiteSpace(c.JoinLink) || b.LinkSentAt is not null) return;
        var (subject, body) = EventEmails.JoinLink(user, c, b);
        if (await email.SendAsync(user.Email, subject, body)) { b.LinkSentAt = DateTime.UtcNow; await db.SaveChangesAsync(); }
    }

    /// <summary>Emails the joining link to everyone with a confirmed ticket who has not had it yet (used when the link is added or changed).</summary>
    public async Task SendJoinLinksAsync(int classId)
    {
        var c = await db.Classes.FirstOrDefaultAsync(x => x.Id == classId);
        if (c is null || c.Format != EventKinds.Online || string.IsNullOrWhiteSpace(c.JoinLink)) return;
        var rows = await db.Bookings.Where(b => b.ClassId == classId && b.Status == BookingStatus.Booked && b.LinkSentAt == null).ToListAsync();
        foreach (var b in rows)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == b.UserId);
            if (user is not null) await SendJoinLinkAsync(b, c, user);
        }
    }

    // ---- PayFast says what happened to the payment ----

    public async Task<bool> ApplyPaymentResultAsync(int bookingId, string paymentStatus, decimal amountGross, string? pfPaymentId)
    {
        var b = await db.Bookings.FirstOrDefaultAsync(x => x.Id == bookingId);
        if (b is null) return false;
        var c = await db.Classes.FirstAsync(x => x.Id == b.ClassId);
        var user = await db.Users.FirstAsync(u => u.Id == b.UserId);

        switch (paymentStatus.ToUpperInvariant())
        {
            case "COMPLETE":
                if (pfPaymentId is not null && b.PayFastPaymentId == pfPaymentId) return true;   // PayFast sent the same notice twice
                if (amountGross != b.AmountDue) return false;                                    // the amount must be what the ticket costs
                if (b.Status == BookingStatus.PendingPayment)
                {
                    await ConfirmAsync(b, c, user, amountGross, pfPaymentId);
                    return true;
                }
                // The hold ran out before the payment arrived. Never double-book: queue a refund and tell both sides.
                b.AmountPaid = amountGross; b.PayFastPaymentId = pfPaymentId;
                b.RefundStatus = "REQUESTED"; b.RefundAmount = amountGross;
                await db.SaveChangesAsync();
                await notifications.AddAsync(user.Id, "BOOKING", "Your ticket wasn't confirmed",
                    $"Your payment for {EventEmails.Describe(c)} arrived after the ticket hold ended, so the place was released. We'll refund {EventEmails.Rand(amountGross)}.", "bookings");
                await notifications.AddForAdminsAsync("INFO", "Refund needed",
                    $"{user.FullName} paid {EventEmails.Rand(amountGross)} for {c.Name} after the ticket hold ended. Refund it on PayFast.", "admin/events");
                return true;
            case "FAILED":
            case "CANCELLED":
                if (b.Status == BookingStatus.PendingPayment) await ReleaseHoldAsync(b);
                return true;
            default:
                return true;
        }
    }

    /// <summary>Gives a pending ticket back (payment abandoned, failed or timed out) and offers the place to the waiting list.</summary>
    private async Task ReleaseHoldAsync(Booking b)
    {
        await ReleaseCreditAsync(b);
        b.Status = BookingStatus.Cancelled; b.HoldUntil = null;
        await db.SaveChangesAsync();
        await PromoteWaitlistAsync(b.ClassId);
    }

    // ---- Waiting list ----

    public async Task<(bool Ok, string? Error)> JoinWaitlistAsync(Guid userId, int classId)
    {
        var c = await db.Classes.FirstOrDefaultAsync(x => x.Id == classId);
        if (c is null || c.Status != "SCHEDULED" || c.StartTime <= DateTime.UtcNow) return (false, "That event is not available.");
        if (!c.WaitlistEnabled) return (false, "This event has no waiting list.");
        var now = DateTime.UtcNow;
        if (now >= c.StartTime.AddHours(-c.SalesCloseHours)) return (false, "Ticket sales for this event have closed.");
        if (await db.Bookings.AnyAsync(b => b.UserId == userId && b.ClassId == classId &&
                (b.Status == BookingStatus.Booked || b.Status == BookingStatus.Waitlisted || b.Status == BookingStatus.PendingPayment)))
            return (false, "You already have a ticket, a held ticket or a waiting-list place for this event.");
        if (await TakenAsync(classId, now) < c.Capacity) return (false, "There are places left, so you can buy a ticket now.");

        db.Bookings.Add(new Booking { UserId = userId, ClassId = classId, Status = BookingStatus.Waitlisted });
        await db.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>When a place frees up, offers it to the person who has waited longest, and holds it for them for a while.</summary>
    public async Task PromoteWaitlistAsync(int classId)
    {
        var c = await db.Classes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == classId);
        if (c is null || c.Status != "SCHEDULED" || !c.WaitlistEnabled) return;
        var now = DateTime.UtcNow;
        if (now >= c.StartTime.AddHours(-c.SalesCloseHours)) return;
        while (await TakenAsync(classId, now) < c.Capacity)
        {
            var waiting = await db.Bookings.Where(b => b.ClassId == classId && b.Status == BookingStatus.Waitlisted &&
                    (b.HoldUntil == null || b.HoldUntil <= now)).ToListAsync();
            if (waiting.Count == 0) return;
            // Priority booking: members of the second plan (and above) are offered a place first. Within each group, whoever waited longest goes first.
            var tiers = new Dictionary<Guid, int>();
            foreach (var userId in waiting.Select(b => b.UserId).Distinct()) tiers[userId] = await TierAsync(userId);
            var next = waiting.OrderBy(b => tiers[b.UserId] >= 2 ? 0 : 1).ThenBy(b => b.BookingDate).ThenBy(b => b.Id).First();
            next.HoldUntil = now.AddHours(_o.WaitlistOfferHours);
            await db.SaveChangesAsync();
            await notifications.AddAsync(next.UserId, "BOOKING", "A place opened up",
                $"A spot is free for {EventEmails.Describe(c)}. It's held for you for {_o.WaitlistOfferHours} hours. Open the event to buy your ticket.", "classes");
        }
    }

    // ---- Technical problems with an online class (the client's "Online Classes: Technical Issues and Disruptions" policy) ----

    /// <summary>
    /// A participant reports a significant technical problem with an online class they booked. It has to be after the class has started
    /// and, where practicable, within 48 hours. The studio gets an email and an in-app alert; the participant gets a receipt.
    /// What is offered (replacement class, refund or credit) is decided by a person, as the policy says.
    /// </summary>
    public async Task<(bool Ok, string? Error)> ReportIssueAsync(Guid userId, int bookingId, string? description)
    {
        var b = await db.Bookings.Include(x => x.Class).FirstOrDefaultAsync(x => x.Id == bookingId && x.UserId == userId);
        if (b?.Class is null || b.Status != BookingStatus.Booked) return (false, "We couldn't find that booking.");
        var c = b.Class; var now = DateTime.UtcNow;
        if (c.Format != EventKinds.Online) return (false, "Technical problems can be reported for online classes.");
        if (now < c.StartTime) return (false, "You can report a problem once the class has started.");
        if (now > c.EndTime.AddHours(48)) return (false, $"Problems should be reported within 48 hours of the class. Please contact us on {_o.ContactEmail}.");
        var text = (description ?? "").Trim();
        if (text.Length < 10) return (false, "Please tell us what happened and about how long it lasted.");
        if (text.Length > 1000) text = text[..1000];
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        var reference = EventEmails.Reference(b.Id);

        await email.SendAsync(_o.ContactEmail, $"Technical problem reported: {c.Name} ({reference})",
            $"{user.FullName} ({user.Email}) reported a technical problem.\n\nEvent: {c.Name}\nDate and time: {EventEmails.When(c)}\nBooking reference: {reference}\n\nWhat happened:\n{text}\n");
        await email.SendAsync(user.Email, $"We've received your report: {c.Name} ({reference})",
            $"Hi {user.FullName.Split(' ')[0]},\n\nThank you for telling us about the problem with {c.Name}. We'll look into it, work out how much it affected the class, and email you with what we can offer, which may be a replacement class, a refund or account credit.\n\nBooking reference: {reference}\nRhythm & Flow: {_o.ContactEmail}, WhatsApp {_o.ContactWhatsApp}\n\n{EventEmails.SignOff}\n");
        await notifications.AddForAdminsAsync("CLASS", "Technical problem reported", $"{user.FullName} reported a problem with {c.Name}: {text}", "admin");
        return (true, null);
    }

    /// <summary>An administrator gives a participant account credit (for example after a technical problem). It is valid for the usual 12 months.</summary>
    public async Task<(bool Ok, string? Error)> GiveCreditAsync(int bookingId, decimal amount, string? reason)
    {
        var b = await db.Bookings.Include(x => x.Class).FirstOrDefaultAsync(x => x.Id == bookingId);
        if (b?.Class is null) return (false, "We couldn't find that booking.");
        var paid = Total(b);
        if (paid <= 0) return (false, "This booking was free, so there is nothing to give credit for.");
        if (amount < 1) return (false, "Enter an amount of at least R1.");
        if (amount > paid) return (false, $"The most for this booking is {EventEmails.Rand(paid)}, what was paid.");
        var why = string.IsNullOrWhiteSpace(reason) ? "Online class problem" : reason.Trim();
        if (why.Length > 200) why = why[..200];
        await IssueCreditAsync(b.UserId, amount, why, b.Id);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == b.UserId);
        if (user is not null)
            await email.SendAsync(user.Email, $"Account credit added: {EventEmails.Rand(amount)}",
                $"Hi {user.FullName.Split(' ')[0]},\n\nWe've added {EventEmails.Rand(amount)} account credit for {b.Class.Name} ({EventEmails.Reference(b.Id)}). Reason: {why}.\nIt is valid until {EventEmails.Sast(DateTime.UtcNow.AddMonths(_o.CreditMonths)):d MMMM yyyy} and is used automatically when you buy a ticket for another event.\n\n{EventEmails.SignOff}\n");
        return (true, null);
    }

    // ---- Credit ----

    public async Task<decimal> CreditBalanceAsync(Guid userId, DateTime now) =>
        (await db.Credits.AsNoTracking().Where(x => x.UserId == userId).ToListAsync()).Where(x => x.ExpiresAt > now).Sum(x => x.Remaining);

    public async Task<CreditsDto> CreditsAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        var rows = (await db.Credits.AsNoTracking().Where(x => x.UserId == userId).ToListAsync())
            .Where(x => x.ExpiresAt > now && x.Remaining > 0).OrderBy(x => x.ExpiresAt).ToList();
        return new CreditsDto(rows.Sum(x => x.Remaining),
            rows.Select(x => new CreditDto(x.Id, x.Amount, x.Remaining, x.IssuedAt, x.ExpiresAt, x.Reason)).ToList());
    }

    private async Task IssueCreditAsync(Guid userId, decimal amount, string reason, int bookingId)
    {
        if (amount <= 0) return;
        var now = DateTime.UtcNow;
        db.Credits.Add(new AccountCredit
        {
            UserId = userId, Amount = amount, Remaining = amount, IssuedAt = now, ExpiresAt = now.AddMonths(_o.CreditMonths),
            Reason = reason, BookingId = bookingId,
        });
        await db.SaveChangesAsync();
        await notifications.AddAsync(userId, "PAYMENT", "Account credit added",
            $"You have {EventEmails.Rand(amount)} credit for a future event. It's valid until {EventEmails.Sast(now.AddMonths(_o.CreditMonths)):d MMM yyyy}.", "bookings");
    }

    /// <summary>Uses credit that expires soonest first. Returns how much was used and a note of which credits, so it can be given back.</summary>
    private async Task<(decimal Applied, string? Uses)> ConsumeCreditAsync(Guid userId, decimal max, DateTime now)
    {
        var credits = (await db.Credits.Where(x => x.UserId == userId).ToListAsync())
            .Where(x => x.ExpiresAt > now && x.Remaining > 0).OrderBy(x => x.ExpiresAt).ThenBy(x => x.Id).ToList();
        var left = max; var uses = new List<string>();
        foreach (var credit in credits)
        {
            if (left <= 0) break;
            var take = Math.Min(credit.Remaining, left);
            credit.Remaining -= take; left -= take;
            uses.Add($"{credit.Id}:{take.ToString(CultureInfo.InvariantCulture)}");
        }
        return (max - left, uses.Count == 0 ? null : string.Join(';', uses));
    }

    private async Task ReleaseCreditAsync(Booking b)
    {
        if (!string.IsNullOrEmpty(b.CreditUses))
            foreach (var part in b.CreditUses.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var bits = part.Split(':');
                if (bits.Length != 2 || !int.TryParse(bits[0], out var id) ||
                    !decimal.TryParse(bits[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)) continue;
                var credit = await db.Credits.FirstOrDefaultAsync(x => x.Id == id);
                if (credit is not null) credit.Remaining = Math.Min(credit.Amount, credit.Remaining + amount);
            }
        b.CreditUses = null; b.CreditApplied = 0;
    }

    // ---- Cancelling ----

    /// <summary>
    /// A customer cancels a paid ticket. With at least 48 hours to go they choose a refund or account credit for the full fee.
    /// Closer than that it becomes a request for an administrator to decide (up to half the fee).
    /// </summary>
    public async Task<(ActionResultDto? Result, string? Error)> CancelAsync(Guid userId, int bookingId, string? choice, string? note)
    {
        var b = await db.Bookings.Include(x => x.Class).FirstOrDefaultAsync(x => x.Id == bookingId && x.UserId == userId);
        if (b is null) return (null, "Booking not found.");
        var c = b.Class!;

        if (b.Status == BookingStatus.Waitlisted || b.Status == BookingStatus.PendingPayment)
        {
            await ReleaseCreditAsync(b);
            b.Status = BookingStatus.Cancelled; b.HoldUntil = null;
            await db.SaveChangesAsync();
            await PromoteWaitlistAsync(c.Id);
            return (new ActionResultDto("Cancelled. Nothing was charged.", BookingStatus.Cancelled), null);
        }
        if (b.Status != BookingStatus.Booked) return (null, "This booking can no longer be cancelled.");
        if (c.StartTime <= DateTime.UtcNow) return (null, "This event has already started.");
        if (b.RequestStatus == "PENDING") return (null, "Your cancellation request is already with us.");
        if (Total(b) <= 0) return (null, "This is a free booking. Cancel it from My bookings.");

        var picked = (choice ?? "").Trim().ToUpperInvariant();
        if (picked is not ("REFUND" or "CREDIT")) return (null, "Choose a refund or account credit.");
        if (picked == "REFUND" && b.AmountPaid <= 0) picked = "CREDIT";   // it was paid with credit only, so credit is what goes back

        var hoursLeft = (c.StartTime - DateTime.UtcNow).TotalHours;
        if (hoursLeft >= _o.FullRefundHours)
        {
            await CancelWithRemedyAsync(b, c, picked, Total(b), "Cancelled at least 48 hours before the event");
            return (new ActionResultDto(picked == "CREDIT"
                ? $"Cancelled. {EventEmails.Rand(Total(b))} has been added to your account credit."
                : $"Cancelled. We'll refund {EventEmails.Rand(b.AmountPaid)} to your original payment method" +
                  (b.CreditApplied > 0 ? $" and return {EventEmails.Rand(b.CreditApplied)} as account credit." : "."), BookingStatus.Cancelled), null);
        }

        b.RequestStatus = "PENDING"; b.RequestChoice = picked; b.RequestedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        await notifications.AddForAdminsAsync("INFO", "Late cancellation request",
            $"{user.FullName} asked to cancel {c.Name} with less than {_o.FullRefundHours} hours to go.{(string.IsNullOrWhiteSpace(note) ? "" : " Note: " + note.Trim())}", "admin/events");
        return (new ActionResultDto(
            $"Your event starts in less than {_o.FullRefundHours} hours, so a refund or credit isn't guaranteed. We've sent your request and will handle it fairly. Up to {_o.LateCancelMaxPercent}% of the fee can be agreed.",
            "REQUESTED"), null);
    }

    /// <summary>Cancels a confirmed ticket and gives back <paramref name="amount"/> as credit, or as a refund request for the cash part.</summary>
    private async Task CancelWithRemedyAsync(Booking b, ClassSession c, string choice, decimal amount, string reason)
    {
        b.Status = BookingStatus.Cancelled; b.ChoiceNeeded = null; b.HoldUntil = null;
        if (choice == "REFUND")
        {
            var cash = Math.Min(amount, b.AmountPaid);
            if (cash > 0) { b.RefundStatus = "REQUESTED"; b.RefundAmount = cash; }
            await db.SaveChangesAsync();
            await IssueCreditAsync(b.UserId, amount - cash, reason, b.Id);   // any part that was paid with credit goes back as credit
            if (cash > 0)
            {
                var user = await db.Users.FirstAsync(u => u.Id == b.UserId);
                await notifications.AddForAdminsAsync("INFO", "Refund to process",
                    $"Refund {EventEmails.Rand(cash)} to {user.FullName} for {c.Name} ({EventEmails.Reference(b.Id)}) on PayFast.", "admin/events");
            }
        }
        else
        {
            await db.SaveChangesAsync();
            await IssueCreditAsync(b.UserId, amount, reason, b.Id);
        }
        await PromoteWaitlistAsync(c.Id);
    }

    /// <summary>The customer's answer after the studio cancelled or moved an event: keep the booking (moved events only), or take a refund or credit.</summary>
    public async Task<(ActionResultDto? Result, string? Error)> ChooseAsync(Guid userId, int bookingId, string choice)
    {
        var b = await db.Bookings.Include(x => x.Class).FirstOrDefaultAsync(x => x.Id == bookingId && x.UserId == userId);
        if (b is null || b.ChoiceNeeded is null) return (null, "There is nothing to choose for this booking.");
        var picked = choice.Trim().ToUpperInvariant();

        if (picked == "KEEP")
        {
            if (b.ChoiceNeeded != "EVENT_RESCHEDULED") return (null, "This event was cancelled, so choose a refund or account credit.");
            b.ChoiceNeeded = null;
            await db.SaveChangesAsync();
            return (new ActionResultDto("Great, your booking stays as it is, now on the new date.", BookingStatus.Booked), null);
        }
        if (picked is not ("REFUND" or "CREDIT")) return (null, "Choose keep, refund or account credit.");
        if (picked == "REFUND" && b.AmountPaid <= 0) picked = "CREDIT";

        var total = Total(b);
        var reason = b.ChoiceNeeded == "EVENT_CANCELLED" ? "Event cancelled by Rhythm & Flow" : "Event rescheduled by Rhythm & Flow";
        await CancelWithRemedyAsync(b, b.Class!, picked, total, reason);
        return (new ActionResultDto(picked == "CREDIT"
            ? $"Done. {EventEmails.Rand(total)} has been added to your account credit."
            : $"Done. We'll refund {EventEmails.Rand(b.AmountPaid)} to your original payment method.", BookingStatus.Cancelled), null);
    }

    // ---- The studio cancels or moves an event ----

    /// <summary>Everyone with a paid ticket gets a full refund or credit, their choice. Free bookings are simply cancelled.</summary>
    public async Task CancelEventAsync(ClassSession c)
    {
        var rows = await db.Bookings.Where(b => b.ClassId == c.Id &&
            (b.Status == BookingStatus.Booked || b.Status == BookingStatus.PendingPayment || b.Status == BookingStatus.Waitlisted)).ToListAsync();
        var freeIds = new List<Guid>(); var paidIds = new List<Guid>();
        foreach (var b in rows)
        {
            if (b.Status == BookingStatus.Booked && Total(b) > 0)
            {
                b.Status = BookingStatus.EventCancelled; b.ChoiceNeeded = "EVENT_CANCELLED"; b.RequestStatus = null; paidIds.Add(b.UserId);
            }
            else
            {
                await ReleaseCreditAsync(b);
                if (b.Status == BookingStatus.Booked) freeIds.Add(b.UserId);
                b.Status = BookingStatus.Cancelled; b.HoldUntil = null;
            }
        }
        notifications.Stage(freeIds, "CLASS", "Class cancelled", $"Sorry, {c.Name} on {EventEmails.Sast(c.StartTime):ddd d MMM} has been cancelled.", "classes");
        notifications.Stage(paidIds, "BOOKING", "Event cancelled: choose a refund or credit",
            $"Sorry, {c.Name} on {EventEmails.Sast(c.StartTime):ddd d MMM} has been cancelled. Open My bookings to choose a full refund or account credit.", "bookings");
        await db.SaveChangesAsync();
        await notifications.PushStagedAsync();
    }

    /// <summary>The time of a paid event changed: ticket holders choose to keep their place or take a refund or credit.</summary>
    public async Task EventRescheduledAsync(ClassSession c)
    {
        var rows = await db.Bookings.Where(b => b.ClassId == c.Id && b.Status == BookingStatus.Booked).ToListAsync();
        var paid = new List<Guid>();
        foreach (var b in rows.Where(x => Total(x) > 0)) { b.ChoiceNeeded = "EVENT_RESCHEDULED"; paid.Add(b.UserId); }
        notifications.Stage(paid, "BOOKING", "Event rescheduled: your choice",
            $"{c.Name} has moved to {EventEmails.Sast(c.StartTime):ddd d MMM} at {EventEmails.Sast(c.StartTime):HH:mm}. Open My bookings to keep your place, or take a refund or account credit.", "bookings");
        await db.SaveChangesAsync();
        await notifications.PushStagedAsync();
    }

    // ---- Administrator: late requests and refunds ----

    public async Task<List<AdminEventRequestDto>> RequestsAsync()
    {
        var rows = await db.Bookings.AsNoTracking().Include(b => b.Class).Where(b => b.RequestStatus == "PENDING" && b.Status == BookingStatus.Booked).ToListAsync();
        var users = await UsersAsync(rows.Select(b => b.UserId));
        return rows.OrderBy(b => b.Class!.StartTime).Select(b => new AdminEventRequestDto(
            b.Id, EventEmails.Reference(b.Id), Name(users, b.UserId), Email(users, b.UserId), b.Class!.Name, b.Class.StartTime,
            Total(b), Math.Round(Total(b) * _o.LateCancelMaxPercent / 100m, 2), b.RequestChoice, b.RequestedAt)).ToList();
    }

    /// <summary>Approve a late cancellation (an amount up to half the fee) or decline it.</summary>
    public async Task<(bool Ok, string? Error)> ResolveRequestAsync(int bookingId, bool approve, decimal? amount)
    {
        var b = await db.Bookings.Include(x => x.Class).FirstOrDefaultAsync(x => x.Id == bookingId && x.RequestStatus == "PENDING");
        if (b is null) return (false, "That request was not found.");
        var c = b.Class!;
        if (!approve)
        {
            b.RequestStatus = "DECLINED";
            await db.SaveChangesAsync();
            await notifications.AddAsync(b.UserId, "BOOKING", "Cancellation request",
                $"We couldn't agree a refund or credit for {EventEmails.Describe(c)} this close to the event, so your booking stays as it is. Contact us on {_o.ContactEmail} or WhatsApp {_o.ContactWhatsApp} if you'd like to talk it through.", "bookings");
            return (true, null);
        }
        var max = Math.Round(Total(b) * _o.LateCancelMaxPercent / 100m, 2);
        var give = amount ?? max;
        if (give <= 0 || give > max) return (false, $"The amount must be more than R0 and no more than {EventEmails.Rand(max)} ({_o.LateCancelMaxPercent}% of the fee).");
        b.RequestStatus = "APPROVED";
        await CancelWithRemedyAsync(b, c, b.RequestChoice ?? "CREDIT", give, "Late cancellation agreed");
        await notifications.AddAsync(b.UserId, "BOOKING", "Cancellation approved",
            $"Your booking for {EventEmails.Describe(c)} is cancelled. We agreed {EventEmails.Rand(give)}{(b.RequestChoice == "REFUND" ? " as a refund" : " as account credit")}.", "bookings");
        return (true, null);
    }

    public async Task<List<AdminRefundDto>> RefundsAsync()
    {
        var rows = await db.Bookings.AsNoTracking().Include(b => b.Class).Where(b => b.RefundStatus == "REQUESTED").ToListAsync();
        var users = await UsersAsync(rows.Select(b => b.UserId));
        return rows.OrderBy(b => b.Id).Select(b => new AdminRefundDto(
            b.Id, EventEmails.Reference(b.Id), Name(users, b.UserId), Email(users, b.UserId), b.Class!.Name, b.RefundAmount, b.PayFastPaymentId)).ToList();
    }

    /// <summary>The administrator has paid the refund on PayFast; this records it and tells the customer.</summary>
    public async Task<(bool Ok, string? Error)> MarkRefundedAsync(int bookingId)
    {
        var b = await db.Bookings.Include(x => x.Class).FirstOrDefaultAsync(x => x.Id == bookingId && x.RefundStatus == "REQUESTED");
        if (b is null) return (false, "That refund was not found.");
        b.RefundStatus = "REFUNDED";
        await db.SaveChangesAsync();
        await notifications.AddAsync(b.UserId, "PAYMENT", "Refund sent",
            $"We've refunded {EventEmails.Rand(b.RefundAmount)} for {EventEmails.Describe(b.Class!)}. It can take a few days to show on your statement.", "bookings");
        var refunded = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == b.UserId);
        if (refunded is not null)
        {
            var mail = PaymentEmails.RefundSent(refunded, EventEmails.Describe(b.Class!), b.RefundAmount, _o);
            await email.SendAsync(refunded.Email, mail.Subject, mail.Body);
        }
        return (true, null);
    }

    private async Task<Dictionary<Guid, User>> UsersAsync(IEnumerable<Guid> ids)
    {
        var list = ids.Distinct().ToList();
        return await db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
    }
    private static string Name(Dictionary<Guid, User> u, Guid id) => u.TryGetValue(id, out var x) ? x.FullName : "Unknown";
    private static string Email(Dictionary<Guid, User> u, Guid id) => u.TryGetValue(id, out var x) ? x.Email : "";

    // ---- Housekeeping: run every few minutes ----

    public async Task SweepAsync(DateTime now)
    {
        // Tickets nobody paid for are released, and their places offered to the waiting list.
        var lapsed = await db.Bookings.Where(b => b.Status == BookingStatus.PendingPayment && b.HoldUntil < now).ToListAsync();
        var classes = new HashSet<int>();
        foreach (var b in lapsed) { await ReleaseCreditAsync(b); b.Status = BookingStatus.Cancelled; b.HoldUntil = null; classes.Add(b.ClassId); }

        // An offered waiting-list spot that was not taken goes to the next person, and the first person moves to the back.
        var stale = await db.Bookings.Where(b => b.Status == BookingStatus.Waitlisted && b.HoldUntil != null && b.HoldUntil < now).ToListAsync();
        foreach (var b in stale) { b.HoldUntil = null; b.BookingDate = now; classes.Add(b.ClassId); }
        if (lapsed.Count + stale.Count > 0) await db.SaveChangesAsync();
        foreach (var id in classes) await PromoteWaitlistAsync(id);

        await SendRemindersAsync(now);
    }

    /// <summary>The optional pre-event reminders the client asked for: about 24 hours before and about 1 hour before. Paid events only, because free classes already have a reminder on the phone.</summary>
    public async Task SendRemindersAsync(DateTime now)
    {
        var rows = await db.Bookings.Include(b => b.Class)
            .Where(b => b.Status == BookingStatus.Booked && b.Class!.Status == "SCHEDULED" && b.Class.StartTime > now && b.Class.StartTime <= now.AddHours(24)).ToListAsync();
        foreach (var b in rows.Where(x => Total(x) > 0))
        {
            var c = b.Class!;
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == b.UserId);
            if (user is null) continue;
            var minutes = (c.StartTime - now).TotalMinutes;
            if (minutes <= 65 && !b.HourReminderSent)
            {
                b.HourReminderSent = true; b.DayReminderSent = true;
                var (s, body) = EventEmails.Reminder(user, c, b, oneHour: true);
                await email.SendAsync(user.Email, s, body);
                await notifications.AddAsync(user.Id, "CLASS", "Starting soon", $"{c.Name} starts in about an hour.", "bookings");
            }
            else if (minutes > 65 && !b.DayReminderSent)
            {
                b.DayReminderSent = true;
                var (s, body) = EventEmails.Reminder(user, c, b, oneHour: false);
                await email.SendAsync(user.Email, s, body);
                await notifications.AddAsync(user.Id, "CLASS", "Tomorrow", $"{EventEmails.Describe(c)}. See you there.", "bookings");
            }
            await db.SaveChangesAsync();
        }
    }
}
