using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;

namespace RhythmFlow.Api.Services;

/// <summary>Lets administrators correct a class or a lesson after it was created.</summary>
public class ScheduleAdminService(AppDbContext db, NotificationService notifications, EventBookingService? events = null)
{
    // ---- Classes and events ----

    /// <summary>Checks what an administrator typed for a class or event. Returns a message to show, or null if it is fine.</summary>
    public static string? ValidateEvent(ClassUpsert r)
    {
        if (r.EndTime <= r.StartTime) return "End time must be after start time.";
        if (!string.IsNullOrWhiteSpace(r.Kind) && !EventKinds.All.Contains(r.Kind.Trim().ToUpperInvariant())) return "Choose class, workshop, retreat or coaching session.";
        if (!string.IsNullOrWhiteSpace(r.Format) && r.Format.Trim().ToUpperInvariant() is not (EventKinds.Online or EventKinds.InPerson)) return "Choose online or in person.";
        // PayFast does not take payments under R5.
        if (r.Price is > 0 and < 5) return "A ticket price must be R0 (free) or at least R5.";
        if (r.SalesCloseHours < 0) return "Ticket sales can't close before 0 hours ahead.";
        if (r.WaitlistEnabled && r.Price <= 0) return "A waiting list is for paid events. Free classes just show as full.";
        if (r.JoinLink is { Length: > 0 } link && !link.Trim().StartsWith("http", StringComparison.OrdinalIgnoreCase)) return "The joining link must start with https://.";
        return null;
    }

    /// <summary>Copies what an administrator typed onto the class. Does not touch times, so a time change can be detected first.</summary>
    public static void ApplyEvent(ClassSession c, ClassUpsert r)
    {
        c.Name = r.Name.Trim(); c.Description = r.Description?.Trim() ?? ""; c.CoachName = r.CoachName.Trim();
        c.Location = r.Location.Trim(); c.Capacity = r.Capacity;
        c.Kind = string.IsNullOrWhiteSpace(r.Kind) ? EventKinds.Class : r.Kind.Trim().ToUpperInvariant();
        c.Format = string.IsNullOrWhiteSpace(r.Format) ? EventKinds.InPerson : r.Format.Trim().ToUpperInvariant();
        c.Price = r.Price; c.PosterUrl = Blank(r.PosterUrl);
        c.SalesCloseHours = r.SalesCloseHours; c.SalesOpenAt = r.SalesOpenAt?.ToUniversalTime(); c.MemberEarlyHours = r.MemberEarlyHours;
        c.JoinPlatform = Blank(r.JoinPlatform); c.JoinLink = Blank(r.JoinLink); c.PracticalNote = Blank(r.PracticalNote);
        c.WaitlistEnabled = r.WaitlistEnabled;
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public async Task<(bool Ok, int Id, string? Error)> CreateClassAsync(ClassUpsert r)
    {
        var error = ValidateEvent(r);
        if (error is not null) return (false, 0, error);
        var c = new ClassSession { StartTime = r.StartTime.ToUniversalTime(), EndTime = r.EndTime.ToUniversalTime() };
        ApplyEvent(c, r);
        db.Classes.Add(c);
        await db.SaveChangesAsync();
        return (true, c.Id, null);
    }

    public async Task<(bool Ok, bool NotFound, string? Error)> UpdateClassAsync(int id, ClassUpsert r)
    {
        var c = await db.Classes.FindAsync(id);
        if (c is null) return (false, true, null);
        if (c.Status != "SCHEDULED") return (false, false, "A cancelled class can't be edited. Schedule a new one instead.");
        var error = ValidateEvent(r);
        if (error is not null) return (false, false, error);

        var booked = await db.Bookings.Where(b => b.ClassId == id && b.Status == BookingStatus.Booked).Select(b => b.UserId).ToListAsync();
        if (r.Capacity < booked.Count)
            return (false, false, $"{booked.Count} people are already booked, so the capacity can't be lower than {booked.Count}.");
        // The price can't change under people who already paid: they paid the old one and the refund maths relies on it.
        if (c.Price != r.Price && booked.Count > 0)
            return (false, false, "Tickets have already been sold, so the price can't be changed. Create a new event instead.");

        var start = r.StartTime.ToUniversalTime(); var end = r.EndTime.ToUniversalTime();
        var timeChanged = c.StartTime != start || c.EndTime != end;
        var moved = timeChanged || !string.Equals(c.Location, r.Location.Trim(), StringComparison.Ordinal);
        var oldLink = c.JoinLink;

        ApplyEvent(c, r);
        c.StartTime = start; c.EndTime = end;

        // A paid event that moves in time lets each ticket holder choose to keep the place or take a refund or credit.
        // Anything else that moved just tells the people who are booked, so nobody turns up at the old time or place.
        var paidMove = timeChanged && c.Price > 0 && events is not null;
        if (moved && booked.Count > 0 && !paidMove)
            notifications.Stage(booked, "CLASS", "Class updated",
                $"{c.Name} is now on {c.StartTime.AddHours(2):ddd d MMM} at {c.StartTime.AddHours(2):HH:mm} (SA time), {c.Location}.", "bookings");

        await db.SaveChangesAsync();
        if (moved && booked.Count > 0 && !paidMove) await notifications.PushStagedAsync();
        if (paidMove && booked.Count > 0) await events!.EventRescheduledAsync(c);
        if (events is not null && c.JoinLink != oldLink && c.JoinLink is not null) await events.SendJoinLinksAsync(c.Id);
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
