using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;

namespace RhythmFlow.Api.Services;

public class BookingOptions
{
    /// <summary>Customers may cancel until this many hours before the class (placeholder until the client confirms the rule).</summary>
    public int CancelCutoffHours { get; set; } = 2;
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
        var counts = await db.Bookings.Where(b => ids.Contains(b.ClassId) && b.Status == BookingStatus.Booked)
            .GroupBy(b => b.ClassId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var mine = (await db.Bookings.Where(b => b.UserId == userId && ids.Contains(b.ClassId) && b.Status == BookingStatus.Booked)
            .Select(b => b.ClassId).ToListAsync()).ToHashSet();
        return classes.Select(c => ToDto(c, counts.GetValueOrDefault(c.Id), mine.Contains(c.Id))).ToList();
    }

    public async Task<(bool Ok, string? Error, BookingDto? Booking)> BookAsync(Guid userId, int classId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var cls = await db.Classes.FirstOrDefaultAsync(c => c.Id == classId);
        if (cls is null || cls.Status != "SCHEDULED") return (false, "That class is not available.", null);
        if (cls.StartTime <= DateTime.UtcNow) return (false, "That class has already started.", null);
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
        if (b.Class!.StartTime - DateTime.UtcNow < TimeSpan.FromHours(_o.CancelCutoffHours))
            return (false, $"Bookings can only be cancelled up to {_o.CancelCutoffHours} hours before the class.");
        b.Status = BookingStatus.Cancelled;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<List<BookingDto>> MineAsync(Guid userId)
    {
        var now = DateTime.UtcNow;
        // Only valid bookings for classes that have not finished appear as upcoming (BR-14).
        var list = await db.Bookings.Include(b => b.Class)
            .Where(b => b.UserId == userId && b.Status == BookingStatus.Booked && b.Class!.EndTime > now)
            .OrderBy(b => b.Class!.StartTime).ToListAsync();
        return list.Select(ToBookingDto).ToList();
    }

    public static ClassDto ToDto(ClassSession c, int booked, bool mine) => new(
        c.Id, c.Name, c.Description, c.CoachName, c.Location, c.StartTime, c.EndTime, c.Capacity,
        Math.Max(0, c.Capacity - booked), mine, c.Status);

    private BookingDto ToBookingDto(Booking b) => new(
        b.Id, b.ClassId, b.Class!.Name, b.Class.CoachName, b.Class.Location, b.Class.StartTime, b.Class.EndTime, b.Status,
        b.Status == BookingStatus.Booked && b.Class.StartTime - DateTime.UtcNow >= TimeSpan.FromHours(_o.CancelCutoffHours));
}
