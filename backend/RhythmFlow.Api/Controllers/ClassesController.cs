using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Controllers;

/// <summary>Class schedule and bookings. No subscription is needed to book (BR-13).</summary>
[ApiController, Authorize]
public class ClassesController(BookingService bookings) : ApiController
{
    [HttpGet("api/classes")]
    public Task<List<ClassDto>> Upcoming() => bookings.UpcomingAsync(UserId);

    [HttpGet("api/bookings")]
    public Task<List<BookingDto>> Mine() => bookings.MineAsync(UserId);

    [HttpPost("api/classes/{id:int}/book")]
    public async Task<ActionResult<BookingDto>> Book(int id)
    {
        var (ok, error, booking) = await bookings.BookAsync(UserId, id);
        return ok ? Ok(booking) : Conflict(new { error });
    }

    [HttpPost("api/bookings/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var (ok, error) = await bookings.CancelAsync(UserId, id);
        return ok ? NoContent() : Conflict(new { error });
    }
}

[ApiController, Authorize]
[Route("api/journal")]
public class JournalController(AppDbContext db) : ApiController
{
    private static readonly string[] Kinds = ["CHECKIN", "REFLECTION", "GRATITUDE"];

    [HttpGet]
    public async Task<List<JournalDto>> List() =>
        await db.Journal.Where(j => j.UserId == UserId).OrderByDescending(j => j.CreatedAt).Take(100)
            .Select(j => new JournalDto(j.Id, j.Kind, j.Mood, j.Text, j.CreatedAt)).ToListAsync();

    [HttpPost]
    public async Task<ActionResult<JournalDto>> Add(JournalRequest req)
    {
        var kind = req.Kind.ToUpperInvariant();
        if (!Kinds.Contains(kind)) return BadRequest(new { error = "Unknown entry type." });
        var j = new JournalEntry { UserId = UserId, Kind = kind, Mood = req.Mood?.Trim() ?? "", Text = req.Text?.Trim() ?? "" };
        db.Journal.Add(j);
        await db.SaveChangesAsync();
        return new JournalDto(j.Id, j.Kind, j.Mood, j.Text, j.CreatedAt);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var j = await db.Journal.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
        if (j is null) return NotFound();
        db.Journal.Remove(j);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

/// <summary>Administrator functions. Every action requires the ADMIN role (BR-15, FR-22).</summary>
[ApiController, Authorize(Roles = Roles.Admin)]
[Route("api/admin")]
public class AdminController(AppDbContext db, NotificationService notifications) : ApiController
{
    [HttpGet("summary")]
    public async Task<AdminSummaryDto> Summary()
    {
        var now = DateTime.UtcNow;
        var active = await db.Subscriptions.Include(s => s.Plan)
            .Where(s => s.Status == SubscriptionStatus.Active && (s.EndDate == null || s.EndDate > now)).ToListAsync();
        return new AdminSummaryDto(
            await db.Users.CountAsync(u => u.Role == Roles.Customer),
            active.Count,
            await db.Classes.CountAsync(c => c.Status == "SCHEDULED" && c.StartTime > now),
            await db.Bookings.CountAsync(b => b.Status == BookingStatus.Booked && b.Class!.StartTime > now),
            active.Sum(s => s.Plan!.Price),
            await db.ErrorLogs.CountAsync(e => e.Status == "NEW"));
    }

    // ---- Lessons ----
    [HttpPost("lessons")]
    public async Task<ActionResult<int>> CreateLesson(LessonUpsert r)
    {
        if (!await db.Programmes.AnyAsync(p => p.Id == r.ProgrammeId)) return BadRequest(new { error = "Unknown programme." });
        var next = (await db.Lessons.Where(l => l.ProgrammeId == r.ProgrammeId).MaxAsync(l => (int?)l.SequenceNumber) ?? 0) + 1;
        var l = new Lesson { SequenceNumber = next };
        Apply(l, r);
        db.Lessons.Add(l);
        await db.SaveChangesAsync();
        return l.Id;
    }

    [HttpPut("lessons/{id:int}")]
    public async Task<IActionResult> UpdateLesson(int id, LessonUpsert r)
    {
        var l = await db.Lessons.FindAsync(id);
        if (l is null) return NotFound();
        Apply(l, r);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("lessons/{id:int}")]
    public async Task<IActionResult> DeleteLesson(int id)
    {
        var l = await db.Lessons.FindAsync(id);
        if (l is null) return NotFound();
        db.Progress.RemoveRange(db.Progress.Where(p => p.LessonId == id));
        db.Lessons.Remove(l);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static void Apply(Lesson l, LessonUpsert r)
    {
        l.ProgrammeId = r.ProgrammeId; l.Title = r.Title.Trim(); l.Description = r.Description?.Trim() ?? "";
        l.Category = r.Category.Trim(); l.Level = string.IsNullOrWhiteSpace(r.Level) ? "All levels" : r.Level.Trim();
        l.DurationSeconds = r.DurationSeconds; l.VideoProvider = r.VideoProvider.Trim(); l.VideoReference = r.VideoReference.Trim();
        l.IsPreview = r.IsPreview;
    }

    // ---- Classes ----
    [HttpGet("classes")]
    public async Task<List<ClassDto>> Classes()
    {
        var list = await db.Classes.OrderByDescending(c => c.StartTime).Take(100).ToListAsync();
        var counts = await db.Bookings.Where(b => b.Status == BookingStatus.Booked).GroupBy(b => b.ClassId)
            .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        return list.Select(c => BookingService.ToDto(c, counts.GetValueOrDefault(c.Id), false)).ToList();
    }

    [HttpPost("classes")]
    public async Task<ActionResult<int>> CreateClass(ClassUpsert r)
    {
        if (r.EndTime <= r.StartTime) return BadRequest(new { error = "End time must be after start time." });
        var c = new ClassSession();
        Apply(c, r);
        db.Classes.Add(c);
        await db.SaveChangesAsync();
        return c.Id;
    }

    [HttpPut("classes/{id:int}")]
    public async Task<IActionResult> UpdateClass(int id, ClassUpsert r)
    {
        var c = await db.Classes.FindAsync(id);
        if (c is null) return NotFound();
        if (r.EndTime <= r.StartTime) return BadRequest(new { error = "End time must be after start time." });
        Apply(c, r);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("classes/{id:int}/cancel")]
    public async Task<IActionResult> CancelClass(int id)
    {
        var c = await db.Classes.FindAsync(id);
        if (c is null) return NotFound();
        c.Status = "CANCELLED";
        var affected = await db.Bookings.Where(b => b.ClassId == id && b.Status == BookingStatus.Booked).ToListAsync();
        foreach (var b in affected) b.Status = BookingStatus.Cancelled;
        // Tell everyone who had a place so they are not left waiting for a class that is not happening.
        notifications.Stage(affected.Select(b => b.UserId), "CLASS", "Class cancelled",
            $"Sorry, {c.Name} on {c.StartTime:ddd d MMM} has been cancelled.", "classes");
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static void Apply(ClassSession c, ClassUpsert r)
    {
        c.Name = r.Name.Trim(); c.Description = r.Description?.Trim() ?? ""; c.CoachName = r.CoachName.Trim();
        c.Location = r.Location.Trim(); c.StartTime = r.StartTime.ToUniversalTime(); c.EndTime = r.EndTime.ToUniversalTime();
        c.Capacity = r.Capacity;
    }

    // ---- Plans ----
    [HttpGet("plans")]
    public async Task<List<PlanDto>> Plans() => (await db.Plans.ToListAsync()).OrderBy(p => p.Price).Select(ContentController.PlanDtoOf).ToList();

    [HttpPut("plans/{id:int}")]
    public async Task<IActionResult> UpdatePlan(int id, PlanUpsert r)
    {
        var p = await db.Plans.FindAsync(id);
        if (p is null) return NotFound();
        p.Name = r.Name.Trim(); p.Description = r.Description?.Trim() ?? ""; p.Price = r.Price; p.Tier = r.Tier;
        p.Features = r.Features ?? ""; p.Status = string.IsNullOrWhiteSpace(r.Status) ? "ACTIVE" : r.Status;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("plans")]
    public async Task<ActionResult<int>> CreatePlan(PlanUpsert r)
    {
        var p = new SubscriptionPlan { Name = r.Name.Trim(), Description = r.Description?.Trim() ?? "", Price = r.Price, Tier = r.Tier, Features = r.Features ?? "" };
        db.Plans.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    // ---- Error log (reports from the API and the app) ----
    [HttpGet("errors")]
    public async Task<List<ErrorLogDto>> Errors([FromQuery] string status = "NEW")
    {
        var q = db.ErrorLogs.AsQueryable();
        if (status != "ALL") q = q.Where(e => e.Status == status);
        var rows = await q.OrderByDescending(e => e.LastSeen).Take(100).ToListAsync();
        return rows.Select(e => new ErrorLogDto(e.Id, e.Source, e.Message, e.Details, e.Route, e.UserEmail, e.AppVersion, e.Device,
            e.Count, e.FirstSeen, e.LastSeen, e.Status)).ToList();
    }

    [HttpPost("errors/{id:int}/resolve")]
    public async Task<IActionResult> ResolveError(int id)
    {
        var e = await db.ErrorLogs.FindAsync(id);
        if (e is null) return NotFound();
        e.Status = "RESOLVED";
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("errors/resolve-all")]
    public async Task<IActionResult> ResolveAllErrors()
    {
        foreach (var e in await db.ErrorLogs.Where(e => e.Status == "NEW").ToListAsync()) e.Status = "RESOLVED";
        await db.SaveChangesAsync();
        return NoContent();
    }
}