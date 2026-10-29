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
public class ClassesController(BookingService bookings, EventBookingService events) : ApiController
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

    // ---- Paid events (tickets) ----

    /// <summary>What a ticket would cost me right now, after any account credit.</summary>
    [HttpGet("api/classes/{id:int}/quote")]
    public Task<TicketQuoteDto> Quote(int id) => events.QuoteAsync(UserId, id);

    /// <summary>Buys a ticket: returns a PayFast link, or a confirmed booking if account credit covered the price.</summary>
    [HttpPost("api/classes/{id:int}/ticket")]
    public async Task<ActionResult<TicketResponse>> Ticket(int id, TicketRequest? req)
    {
        var (result, error) = await events.TicketAsync(UserId, id, req?.UseCredit ?? true);
        return result is not null ? Ok(result) : Conflict(new { error });
    }

    [HttpPost("api/classes/{id:int}/waitlist")]
    public async Task<IActionResult> Waitlist(int id)
    {
        var (ok, error) = await events.JoinWaitlistAsync(UserId, id);
        return ok ? NoContent() : Conflict(new { error });
    }

    /// <summary>Cancels a paid ticket: a refund or credit choice (full if 48 hours or more ahead, otherwise a request).</summary>
    [HttpPost("api/bookings/{id:int}/cancel-ticket")]
    public async Task<ActionResult<ActionResultDto>> CancelTicket(int id, CancelRequestDto req)
    {
        var (result, error) = await events.CancelAsync(UserId, id, req.Choice, req.Note);
        return result is not null ? Ok(result) : Conflict(new { error });
    }

    /// <summary>The answer after the studio cancelled or moved an event: KEEP, REFUND or CREDIT.</summary>
    [HttpPost("api/bookings/{id:int}/choice")]
    public async Task<ActionResult<ActionResultDto>> Choose(int id, ChoiceRequest req)
    {
        var (result, error) = await events.ChooseAsync(UserId, id, req.Choice);
        return result is not null ? Ok(result) : Conflict(new { error });
    }

    /// <summary>Report a significant technical problem with an online class (within 48 hours of it).</summary>
    [HttpPost("api/bookings/{id:int}/report-issue")]
    public async Task<ActionResult<ActionResultDto>> ReportIssue(int id, ReportIssueRequest req)
    {
        var (ok, error) = await events.ReportIssueAsync(UserId, id, req.Description);
        return ok ? Ok(new ActionResultDto("Thank you. We've received your report and will email you with what we can offer.", "REPORTED")) : BadRequest(new { error });
    }

    [HttpGet("api/credits")]
    public Task<CreditsDto> Credits() => events.CreditsAsync(UserId);
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
public class AdminController(AppContentService appContent, IVideoStorage videoStorage, AppDbContext db, NotificationService notifications, EventBookingService events, UserAdminService users, ProgrammeAdminService programmes, AdminOverviewService overview, PlanAdminService plansAdmin, ScheduleAdminService schedule) : ApiController
{
    // ---- Who is booked, who holds a plan ----
    [HttpGet("bookings")]
    public Task<List<AdminBookingDto>> UpcomingBookings() => overview.UpcomingBookingsAsync();

    [HttpGet("classes/{id:int}/attendees")]
    public async Task<ActionResult<List<AdminAttendeeDto>>> Attendees(int id) =>
        await overview.AttendeesAsync(id) is { } list ? list : NotFound();

    [HttpGet("subscriptions")]
    public Task<List<AdminSubscriptionDto>> ActiveSubscriptions() => overview.ActiveSubscriptionsAsync();

    // ---- Programmes (FR-22) ----
    [HttpGet("programmes")]
    public Task<List<AdminProgrammeDto>> Programmes() => programmes.ListAsync();

    [HttpPost("programmes")]
    public async Task<ActionResult<int>> CreateProgramme(ProgrammeUpsert r)
    {
        var (ok, id, error) = await programmes.CreateAsync(r);
        return ok ? id : BadRequest(new { error });
    }

    [HttpPut("programmes/{id:int}")]
    public async Task<IActionResult> UpdateProgramme(int id, ProgrammeUpsert r)
    {
        var (ok, notFound, error) = await programmes.UpdateAsync(id, r);
        if (notFound) return NotFound();
        return ok ? NoContent() : BadRequest(new { error });
    }

    [HttpDelete("programmes/{id:int}")]
    public async Task<IActionResult> DeleteProgramme(int id)
    {
        var (ok, notFound) = await programmes.DeleteAsync(id);
        return notFound ? NotFound() : NoContent();
    }

    // ---- Customers ----
    [HttpGet("users")]
    public Task<List<AdminUserDto>> Users([FromQuery] string? search) => users.ListAsync(search);

    [HttpPost("users/{id:guid}/status")]
    public async Task<IActionResult> SetUserStatus(Guid id, SetUserStatusRequest req)
    {
        var (ok, notFound, error) = await users.SetStatusAsync(UserId, id, req.Status);
        if (notFound) return NotFound();
        return ok ? NoContent() : BadRequest(new { error });
    }

    /// <summary>Deletes a customer's account and personal data (not an administrator, not yourself).</summary>
    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, [FromServices] AccountService accounts)
    {
        var (ok, notFound, error) = await accounts.AdminDeleteAsync(UserId, id);
        if (notFound) return NotFound();
        return ok ? NoContent() : BadRequest(new { error });
    }

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
            active.Sum(s => PlanPricing.MonthlyEquivalent(s)),
            await db.ErrorLogs.CountAsync(e => e.Status == "NEW"));
    }

    // ---- The wording the client edits herself ----
    [HttpGet("content")]
    public Task<AppContentDto> GetContent() => appContent.GetAsync();

    [HttpPut("content")]
    public async Task<IActionResult> SaveContent(AppContentDto c)
    {
        var (ok, error) = await appContent.SaveAsync(c);
        return ok ? NoContent() : BadRequest(new { error });
    }

    /// <summary>Goes back to the built-in wording.</summary>
    [HttpPost("content/reset")]
    public async Task<IActionResult> ResetContent() { await appContent.ResetAsync(); return NoContent(); }

    // ---- Lessons ----
    /// <summary>Gives the admin a signed link to upload a video straight to the private AWS bucket, and the key to save on the lesson.</summary>
    [HttpPost("videos/upload-url")]
    public ActionResult<VideoUploadResponse> VideoUploadUrl(VideoUploadRequest r)
    {
        var (result, error) = VideoUploads.Create(videoStorage, r);
        return result is not null ? result : StatusCode(videoStorage.Configured ? 400 : 503, new { error });
    }

    [HttpPost("lessons")]
    public async Task<ActionResult<int>> CreateLesson(LessonUpsert r)
    {
        if (!await db.Programmes.AnyAsync(p => p.Id == r.ProgrammeId)) return BadRequest(new { error = "Unknown programme." });
        if (!LessonLevels.TryNormalise(r.Level, out _)) return BadRequest(new { error = LessonLevels.Refusal });
        var video = LessonVideo.Normalise(r.VideoProvider, r.VideoReference);
        if (!video.Ok) return BadRequest(new { error = video.Error });
        r = r with { VideoProvider = video.Provider, VideoReference = video.Reference };
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
        var (ok, notFound, error) = await schedule.UpdateLessonAsync(id, r);
        if (notFound) return NotFound();
        return ok ? NoContent() : BadRequest(new { error });
    }

    [HttpGet("lessons")]
    public Task<List<AdminLessonDto>> AdminLessons() => schedule.LessonsAsync();

    [HttpDelete("lessons/{id:int}")]
    public async Task<IActionResult> DeleteLesson(int id)
    {
        return await schedule.DeleteLessonAsync(id) ? NoContent() : NotFound();
    }

    private static void Apply(Lesson l, LessonUpsert r)
    {
        l.ProgrammeId = r.ProgrammeId; l.Title = r.Title.Trim(); l.Description = r.Description?.Trim() ?? "";
        l.Category = r.Category.Trim(); LessonLevels.TryNormalise(r.Level, out var level); l.Level = level;
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
        return list.Select(c => BookingService.ToDto(c, counts.GetValueOrDefault(c.Id), false) with
            { JoinLink = c.JoinLink, PracticalNote = c.PracticalNote, SalesCloseHours = c.SalesCloseHours }).ToList();
    }

    [HttpPost("classes")]
    public async Task<ActionResult<int>> CreateClass(ClassUpsert r)
    {
        var (ok, id, error) = await schedule.CreateClassAsync(r);
        return ok ? id : BadRequest(new { error });
    }

    [HttpPut("classes/{id:int}")]
    public async Task<IActionResult> UpdateClass(int id, ClassUpsert r)
    {
        var (ok, notFound, error) = await schedule.UpdateClassAsync(id, r);
        if (notFound) return NotFound();
        return ok ? NoContent() : BadRequest(new { error });
    }

    [HttpDelete("classes/{id:int}")]
    public async Task<IActionResult> DeleteClass(int id)
    {
        var (ok, notFound, error) = await schedule.DeleteClassAsync(id);
        if (notFound) return NotFound();
        return ok ? NoContent() : BadRequest(new { error });
    }

    /// <summary>Removes every cancelled or finished class that nobody paid for.</summary>
    [HttpPost("classes/clear-finished")]
    public async Task<ActionResult<object>> ClearFinishedClasses() => new { removed = await schedule.DeleteFinishedClassesAsync() };

    [HttpPost("classes/{id:int}/cancel")]
    public async Task<IActionResult> CancelClass(int id)
    {
        var c = await db.Classes.FindAsync(id);
        if (c is null) return NotFound();
        c.Status = "CANCELLED";
        await db.SaveChangesAsync();
        // Everyone who had a place is told. People who paid are offered a full refund or account credit, their choice.
        await events.CancelEventAsync(c);
        return NoContent();
    }

    // ---- Paid events: late cancellation requests and refunds to process ----
    [HttpGet("event-requests")]
    public Task<List<AdminEventRequestDto>> EventRequests() => events.RequestsAsync();

    [HttpPost("bookings/{id:int}/resolve")]
    public async Task<IActionResult> ResolveRequest(int id, ResolveRequestRequest r)
    {
        var (ok, error) = await events.ResolveRequestAsync(id, r.Approve, r.Amount);
        return ok ? NoContent() : BadRequest(new { error });
    }

    [HttpGet("refunds")]
    public Task<List<AdminRefundDto>> Refunds() => events.RefundsAsync();

    /// <summary>Gives a participant account credit, for example after a technical problem with an online class.</summary>
    [HttpPost("bookings/{id:int}/credit")]
    public async Task<IActionResult> GiveCredit(int id, GiveCreditRequest r)
    {
        var (ok, error) = await events.GiveCreditAsync(id, r.Amount, r.Reason);
        return ok ? NoContent() : BadRequest(new { error });
    }

    [HttpPost("bookings/{id:int}/refunded")]
    public async Task<IActionResult> Refunded(int id)
    {
        var (ok, error) = await events.MarkRefundedAsync(id);
        return ok ? NoContent() : BadRequest(new { error });
    }

    [HttpPost("classes/{id:int}/send-links")]
    public async Task<IActionResult> SendLinks(int id)
    {
        await events.SendJoinLinksAsync(id);
        return NoContent();
    }

    // ---- Plans ----
    [HttpGet("plans")]
    public Task<List<AdminPlanDto>> Plans() => plansAdmin.ListAsync();

    [HttpPut("plans/{id:int}")]
    public async Task<IActionResult> UpdatePlan(int id, PlanUpsert r)
    {
        var (ok, notFound, error) = await plansAdmin.UpdateAsync(id, r);
        if (notFound) return NotFound();
        return ok ? NoContent() : BadRequest(new { error });
    }

    [HttpPost("plans")]
    public async Task<ActionResult<int>> CreatePlan(PlanUpsert r)
    {
        var (ok, id, error) = await plansAdmin.CreateAsync(r);
        return ok ? id : BadRequest(new { error });
    }

    [HttpDelete("plans/{id:int}")]
    public async Task<IActionResult> DeletePlan(int id)
    {
        var (ok, notFound, error) = await plansAdmin.DeleteAsync(id);
        if (notFound) return NotFound();
        return ok ? NoContent() : BadRequest(new { error });
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

    [HttpDelete("errors/{id:int}")]
    public async Task<IActionResult> DeleteError(int id)
    {
        var e = await db.ErrorLogs.FindAsync(id);
        if (e is null) return NotFound();
        db.ErrorLogs.Remove(e);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Clears every error report that has been marked resolved.</summary>
    [HttpPost("errors/clear-resolved")]
    public async Task<IActionResult> ClearResolvedErrors()
    {
        db.ErrorLogs.RemoveRange(await db.ErrorLogs.Where(e => e.Status == "RESOLVED").ToListAsync());
        await db.SaveChangesAsync();
        return NoContent();
    }
}