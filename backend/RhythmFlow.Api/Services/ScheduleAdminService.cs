using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;

namespace RhythmFlow.Api.Services;

/// <summary>Lets administrators correct a class or a lesson after it was created.</summary>
public class ScheduleAdminService(AppDbContext db, NotificationService notifications)
{
    // ---- Classes ----

    public async Task<(bool Ok, bool NotFound, string? Error)> UpdateClassAsync(int id, ClassUpsert r)
    {
        var c = await db.Classes.FindAsync(id);
        if (c is null) return (false, true, null);
        if (c.Status != "SCHEDULED") return (false, false, "A cancelled class can't be edited. Schedule a new one instead.");
        if (r.EndTime <= r.StartTime) return (false, false, "End time must be after start time.");

        var booked = await db.Bookings.Where(b => b.ClassId == id && b.Status == BookingStatus.Booked).Select(b => b.UserId).ToListAsync();
        if (r.Capacity < booked.Count)
            return (false, false, $"{booked.Count} people are already booked, so the capacity can't be lower than {booked.Count}.");

        var start = r.StartTime.ToUniversalTime(); var end = r.EndTime.ToUniversalTime();
        var moved = c.StartTime != start || c.EndTime != end || !string.Equals(c.Location, r.Location.Trim(), StringComparison.Ordinal);

        c.Name = r.Name.Trim(); c.Description = r.Description?.Trim() ?? ""; c.CoachName = r.CoachName.Trim();
        c.Location = r.Location.Trim(); c.StartTime = start; c.EndTime = end; c.Capacity = r.Capacity;

        // Anyone already booked is told when the time or place changed, so nobody turns up at the old one.
        if (moved && booked.Count > 0)
            notifications.Stage(booked, "CLASS", "Class updated",
                $"{c.Name} is now on {c.StartTime.AddHours(2):ddd d MMM} at {c.StartTime.AddHours(2):HH:mm} (SA time), {c.Location}.", "bookings");

        await db.SaveChangesAsync();
        if (moved && booked.Count > 0) await notifications.PushStagedAsync();
        return (true, false, null);
    }

    // ---- Lessons ----

    public async Task<List<AdminLessonDto>> LessonsAsync() =>
        (await db.Lessons.AsNoTracking().Include(l => l.Programme).ToListAsync())
            .OrderBy(l => l.ProgrammeId).ThenBy(l => l.SequenceNumber)
            .Select(l => new AdminLessonDto(l.Id, l.ProgrammeId, l.Programme?.Name ?? "", l.Title, l.Description, l.Category, l.Level,
                l.DurationSeconds, l.VideoProvider, l.VideoReference, l.IsPreview)).ToList();

    public async Task<(bool Ok, bool NotFound, string? Error)> UpdateLessonAsync(int id, LessonUpsert r)
    {
        var l = await db.Lessons.FindAsync(id);
        if (l is null) return (false, true, null);
        if (!await db.Programmes.AnyAsync(p => p.Id == r.ProgrammeId)) return (false, false, "Unknown programme.");

        // Moving a lesson to another programme puts it at the end there.
        if (l.ProgrammeId != r.ProgrammeId)
            l.SequenceNumber = (await db.Lessons.Where(x => x.ProgrammeId == r.ProgrammeId).MaxAsync(x => (int?)x.SequenceNumber) ?? 0) + 1;

        l.ProgrammeId = r.ProgrammeId; l.Title = r.Title.Trim(); l.Description = r.Description?.Trim() ?? "";
        l.Category = r.Category.Trim(); l.Level = string.IsNullOrWhiteSpace(r.Level) ? "All levels" : r.Level.Trim();
        l.DurationSeconds = r.DurationSeconds; l.VideoProvider = r.VideoProvider.Trim(); l.VideoReference = r.VideoReference.Trim();
        l.IsPreview = r.IsPreview;
        await db.SaveChangesAsync();
        return (true, false, null);
    }
}
