using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;

namespace RhythmFlow.Api.Services;

/// <summary>The lists behind the administrator's totals: who is booked into which class and who holds which plan.</summary>
public class AdminOverviewService(AppDbContext db)
{
    /// <summary>Everyone holding a place in one class, earliest booking first. Null when the class does not exist.</summary>
    public async Task<List<AdminAttendeeDto>?> AttendeesAsync(int classId)
    {
        if (!await db.Classes.AnyAsync(c => c.Id == classId)) return null;
        var rows = await db.Bookings.AsNoTracking().Where(b => b.ClassId == classId && b.Status == BookingStatus.Booked).ToListAsync();
        var users = await UsersAsync(rows.Select(b => b.UserId));
        return rows.OrderBy(b => b.BookingDate)
            .Select(b => new AdminAttendeeDto(b.Id, b.UserId, Name(users, b.UserId), Email(users, b.UserId), b.BookingDate, b.Status)).ToList();
    }

    /// <summary>Every current booking for a class that has not started yet, soonest class first.</summary>
    public async Task<List<AdminBookingDto>> UpcomingBookingsAsync()
    {
        var now = DateTime.UtcNow;
        var rows = await db.Bookings.AsNoTracking().Include(b => b.Class)
            .Where(b => b.Status == BookingStatus.Booked && b.Class!.Status == "SCHEDULED" && b.Class.StartTime > now).ToListAsync();
        var users = await UsersAsync(rows.Select(b => b.UserId));
        return rows.OrderBy(b => b.Class!.StartTime).ThenBy(b => b.BookingDate)
            .Select(b => new AdminBookingDto(b.Id, b.ClassId, b.Class!.Name, b.Class.StartTime, b.Class.Location,
                b.UserId, Name(users, b.UserId), Email(users, b.UserId), b.BookingDate, b.Status)).ToList();
    }

    /// <summary>The active plans, newest first. Same rule as the "active plans" total on the admin home, so the two always agree.</summary>
    public async Task<List<AdminSubscriptionDto>> ActiveSubscriptionsAsync()
    {
        var now = DateTime.UtcNow;
        var rows = await db.Subscriptions.AsNoTracking().Include(s => s.Plan)
            .Where(s => s.Status == SubscriptionStatus.Active && (s.EndDate == null || s.EndDate > now)).ToListAsync();
        var users = await UsersAsync(rows.Select(s => s.UserId));
        return rows.OrderByDescending(s => s.StartDate ?? s.CreatedAt)
            .Select(s => new AdminSubscriptionDto(s.Id, s.UserId, Name(users, s.UserId), Email(users, s.UserId), s.Plan!.Name, s.Plan.Price,
                s.Status, s.StartDate, s.EndDate, true)).ToList();
    }

    private async Task<Dictionary<Guid, User>> UsersAsync(IEnumerable<Guid> ids)
    {
        var list = ids.Distinct().ToList();
        return await db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
    }

    private static string Name(Dictionary<Guid, User> u, Guid id) => u.TryGetValue(id, out var x) ? x.FullName : "Unknown";
    private static string Email(Dictionary<Guid, User> u, Guid id) => u.TryGetValue(id, out var x) ? x.Email : "";
}
