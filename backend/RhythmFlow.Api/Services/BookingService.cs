using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;

namespace RhythmFlow.Api.Services;

public class BookingOptions
{
    /// <summary>Customers may cancel until this many hours before the class (placeholder until the client confirms the rule).</summary>
    public int CancelCutoffHours { get; set; } = 2;
    /// <summary>A paid ticket cancelled at least this many hours ahead can be refunded or credited in full (the client's rule).</summary>
    public int FullRefundHours { get; set; } = 48;
}

public class BookingService(AppDbContext db, Microsoft.Extensions.Options.IOptions<BookingOptions> options)
{
    private readonly BookingOptions _o = options.Value;

    public async Task<List<ClassDto>> UpcomingAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        var classes = await db.Classes.Where(c => c.Status == "SCHEDULED" && c.StartTime > now)
            .OrderBy(c => c.StartTime).ToListAsync();
        var ids = classes.Select(c => c.Id).ToList();
        // Places that are taken: confirmed tickets, and tickets or waiting-list offers that are being held right now.
        var counts = await db.Bookings.Where(b => ids.Contains(b.ClassId) &&
                (b.Status == BookingStatus.Booked || ((b.Status == BookingStatus.PendingPayment || b.Status == BookingStatus.Waitlisted) && b.HoldUntil > now)))
            .GroupBy(b => b.ClassId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var mine = (await db.Bookings.Where(b => b.UserId == userId && ids.Contains(b.ClassId) && b.Status == BookingStatus.Booked)
            .Select(b => b.ClassId).ToListAsync()).ToHashSet();
        var waiting = (await db.Bookings.Where(b => b.UserId == userId && ids.Contains(b.ClassId) && b.Status == BookingStatus.Waitlisted)
            .Select(b => b.ClassId).ToListAsync()).ToHashSet();
        var tier = await new EntitlementService(db).GetUserTierAsync(userId);
        // A spot offered to me from the waiting list is held for me, so it does not count against me.
        var offered = (await db.Bookings.Where(b => b.UserId == userId && ids.Contains(b.ClassId) && b.Status == BookingStatus.Waitlisted && b.HoldUntil > now)
            .Select(b => b.ClassId).ToListAsync()).ToHashSet();
        return classes.Select(c =>
        {
            var taken = counts.GetValueOrDefault(c.Id);
            var dto = ToDto(c, offered.Contains(c.Id) ? taken - 1 : taken, mine.Contains(c.Id));
            if (c.Price <= 0) return dto;
            var blocked = mine.Contains(c.Id) ? null : EventBookingService.BuyBlockedReason(c, tier, now, offered.Contains(c.Id) ? 0 : taken);
            // On the waiting list without an offer means "wait", not "buy".
            if (waiting.Contains(c.Id) && !offered.Contains(c.Id)) blocked ??= "You're on the waiting list.";
            return dto with { CanBuyNow = blocked is null, BuyBlockedReason = blocked, OnWaitlist = waiting.Contains(c.Id) };
        }).ToList();
    }

    public async Task<(bool Ok, string? Error, BookingDto? Booking)> BookAsync(Guid userId, int classId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var cls = await db.Classes.FirstOrDefaultAsync(c => c.Id == classId);
        if (cls is null || cls.Status != "SCHEDULED") return (false, "That class is not available.", null);
        if (cls.StartTime <= DateTime.UtcNow) return (false, "That class has already started.", null);
        if (cls.Price > 0) return (false, "This event needs a ticket. Buy one from the event page.", null);
        if (await db.Bookings.AnyAsync(b => b.UserId == userId && b.ClassId == classId && b.Status == BookingStatus.Booked))
            return (false, "You have already booked this class.", null);
        var taken = await db.Bookings.CountAsync(b => b.ClassId == classId && b.Status == BookingStatus.Booked);
        if (taken >= cls.Capacity) return (false, "This class is full.", null); // BR-12

        var booking = new Booking { UserId = userId, ClassId = classId, Status = BookingStatus.Booked };
        db.Bookings.Add(booking);
        try
        {
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateException)
        {
            // Two taps arrived at once: the unique index let one through and stopped the other.
            return (false, "You have already booked this class.", null);
        }
        booking.Class = cls;
        return (true, null, ToBookingDto(booking));
    }

    public async Task<(bool Ok, string? Error)> CancelAsync(Guid userId, int bookingId)
    {
        var b = await db.Bookings.Include(x => x.Class).FirstOrDefaultAsync(x => x.Id == bookingId && x.UserId == userId);
        if (b is null) return (false, "Booking not found.");
        if (b.Status != BookingStatus.Booked) return (false, "This booking can no longer be cancelled.");
        if (b.AmountPaid + b.CreditApplied > 0)
            return (false, "This is a paid ticket, so cancelling it means choosing a refund or account credit.");
        if (b.Class!.StartTime - DateTime.UtcNow < TimeSpan.FromHours(_o.CancelCutoffHours))
            return (false, $"Bookings can only be cancelled up to {_o.CancelCutoffHours} hours before the class.");
        b.Status = BookingStatus.Cancelled;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<List<BookingDto>> MineAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        // Only valid bookings for classes that have not finished appear as upcoming (BR-14). Also shown: waiting-list places, and
        // paid tickets for an event the studio cancelled (the customer still has to choose a refund or credit).
        var list = await db.Bookings.Include(b => b.Class)
            .Where(b => b.UserId == userId && b.Class!.EndTime > now &&
                (b.Status == BookingStatus.Booked || b.Status == BookingStatus.Waitlisted || b.Status == BookingStatus.EventCancelled))
            .OrderBy(b => b.Class!.StartTime).ToListAsync();
        return list.Select(ToBookingDto).ToList();
    }

    public static ClassDto ToDto(ClassSession c, int booked, bool mine) => new(
        c.Id, c.Name, c.Description, c.CoachName, c.Location, c.StartTime, c.EndTime, c.Capacity,
        Math.Max(0, c.Capacity - booked), mine, c.Status,
        c.Kind, c.Format, c.Price, c.PosterUrl, c.SalesOpenAt, c.StartTime.AddHours(-c.SalesCloseHours), c.MemberEarlyHours,
        CanBuyNow: true, BuyBlockedReason: null, OnWaitlist: false, WaitlistEnabled: c.WaitlistEnabled, JoinPlatform: c.JoinPlatform);

    private BookingDto ToBookingDto(Booking b)
    {
        var c = b.Class!;
        var hoursLeft = (c.StartTime - DateTime.UtcNow).TotalHours;
        var paid = b.AmountPaid + b.CreditApplied > 0;
        var live = b.Status == BookingStatus.Booked;
        return new BookingDto(
            b.Id, b.ClassId, c.Name, c.CoachName, c.Location, c.StartTime, c.EndTime, b.Status,
            CanCancel: live && (paid ? b.RequestStatus != "PENDING" : hoursLeft >= _o.CancelCutoffHours) || b.Status == BookingStatus.Waitlisted,
            Reference: EventEmails.Reference(b.Id), Kind: c.Kind, Format: c.Format, TotalPaid: b.AmountPaid + b.CreditApplied,
            JoinPlatform: live && c.Format == EventKinds.Online ? c.JoinPlatform : null,
            JoinLink: live && c.Format == EventKinds.Online ? c.JoinLink : null,
            ChoiceNeeded: b.ChoiceNeeded, RequestStatus: b.RequestStatus,
            FreeCancellation: live && paid && hoursLeft >= _o.FullRefundHours, RefundStatus: b.RefundStatus);
    }
}
