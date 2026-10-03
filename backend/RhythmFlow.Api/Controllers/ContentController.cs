using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Controllers;

[ApiController]
public class ContentController(
    AppDbContext db, EntitlementService entitlement, MediaTokenService media, ProgressService progress,
    IHttpClientFactory http, IOptions<MediaOptions> mediaOptions) : ApiController
{
    [HttpGet("api/plans")]
    public async Task<List<PlanDto>> Plans() =>
        // Sorted in memory: SQLite cannot order decimals in the database.
        (await db.Plans.Where(p => p.Status == "ACTIVE").ToListAsync()).OrderBy(p => p.Price)
        .Select(PlanDtoOf).ToList();

    public static PlanDto PlanDtoOf(SubscriptionPlan p) => new(p.Id, p.Name, p.Description, p.Price, p.BillingFrequency, p.Tier,
        p.Features.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList());

    [Authorize, HttpGet("api/programmes")]
    public async Task<List<ProgrammeDto>> Programmes()
    {
        var tier = await entitlement.GetUserTierAsync(UserId);
        var list = await db.Programmes.Include(p => p.Lessons).Where(p => p.Status == "ACTIVE").OrderBy(p => p.Id).ToListAsync();
        return list.Select(p => new ProgrammeDto(p.Id, p.Name, p.Description, p.MinTier,
            !IsAdmin && tier < p.MinTier, p.Lessons.Count)).ToList();
    }

    [Authorize, HttpGet("api/lessons")]
    public async Task<List<LessonDto>> Lessons([FromQuery] string? category, [FromQuery] string? q, [FromQuery] int? programmeId)
    {
        var tier = await entitlement.GetUserTierAsync(UserId);
        var query = db.Lessons.Include(l => l.Programme).Where(l => l.Programme!.Status == "ACTIVE");
        if (programmeId is not null) query = query.Where(l => l.ProgrammeId == programmeId);
        if (!string.IsNullOrWhiteSpace(category) && category != "All") query = query.Where(l => l.Category == category);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(l => EF.Functions.Like(l.Title, $"%{q}%") || EF.Functions.Like(l.Description, $"%{q}%"));
        var lessons = await query.OrderBy(l => l.ProgrammeId).ThenBy(l => l.SequenceNumber).ToListAsync();
        var prog = await db.Progress.Where(p => p.UserId == UserId).ToDictionaryAsync(p => p.LessonId);
        return lessons.Select(l => ToDto(l, tier, prog.GetValueOrDefault(l.Id))).ToList();
    }

    [Authorize, HttpGet("api/lessons/{id:int}")]
    public async Task<ActionResult<LessonDto>> Lesson(int id)
    {
        var l = await db.Lessons.Include(x => x.Programme).FirstOrDefaultAsync(x => x.Id == id);
        if (l is null) return NotFound();
        var tier = await entitlement.GetUserTierAsync(UserId);
        var p = await db.Progress.FirstOrDefaultAsync(x => x.UserId == UserId && x.LessonId == id);
        return ToDto(l, tier, p);
    }

    /// <summary>Entitlement check, then a short-lived signed link (FR-09, FR-11, FR-12).</summary>
    [Authorize, HttpGet("api/lessons/{id:int}/playback")]
    public async Task<ActionResult<PlaybackDto>> Playback(int id)
    {
        var l = await db.Lessons.Include(x => x.Programme).FirstOrDefaultAsync(x => x.Id == id);
        if (l is null) return NotFound();
        var tier = await entitlement.GetUserTierAsync(UserId);
        if (!EntitlementService.CanWatch(l, tier, IsAdmin))
            return StatusCode(403, new { error = "An active subscription is required to watch this lesson." });

        var (query, expires) = media.Sign(l.Id, UserId);
        var url = $"{Request.Scheme}://{Request.Host}/media/{l.Id}?{query}";
        var resume = (await db.Progress.FirstOrDefaultAsync(x => x.UserId == UserId && x.LessonId == id))?.WatchTimeSeconds ?? 0;
        if (resume >= l.DurationSeconds - 3) resume = 0;
        return new PlaybackDto(url, expires, resume);
    }

    [Authorize, HttpPost("api/progress")]
    public async Task<ActionResult<ProgressDto>> UpdateProgress(ProgressUpdateRequest req)
    {
        var l = await db.Lessons.Include(x => x.Programme).FirstOrDefaultAsync(x => x.Id == req.LessonId);
        if (l is null) return NotFound();
        var tier = await entitlement.GetUserTierAsync(UserId);
        if (!EntitlementService.CanWatch(l, tier, IsAdmin)) return StatusCode(403, new { error = "Subscription required." });
        return await progress.UpdateAsync(UserId, l, req.WatchTimeSeconds);
    }

    [Authorize, HttpGet("api/progress/summary")]
    public Task<ProgressSummaryDto> Summary() => progress.SummaryAsync(UserId);

    // ---- Media streaming: signed link, per-user, expires, never reveals the origin URL ----
    [AllowAnonymous, HttpGet("media/{lessonId:int}")]
    public async Task<IActionResult> Media(int lessonId, [FromQuery] Guid u, [FromQuery] long e, [FromQuery] string s, CancellationToken ct)
    {
        if (!media.Validate(lessonId, u, e, s)) return Unauthorized();
        var lesson = await db.Lessons.FindAsync([lessonId], ct);
        if (lesson is null) return NotFound();

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["Content-Disposition"] = "inline";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        if (lesson.VideoProvider.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            var root = Path.GetFullPath(mediaOptions.Value.LocalFolder);
            var path = Path.GetFullPath(Path.Combine(root, lesson.VideoReference));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(path)) return NotFound();
            return PhysicalFile(path, "video/mp4", enableRangeProcessing: true);
        }

        // Remote: stream through this API (forwarding Range requests) so the origin URL is never given to the client.
        var client = http.CreateClient("video");
        using var req = new HttpRequestMessage(HttpMethod.Get, lesson.VideoReference);
        if (Request.Headers.TryGetValue("Range", out var range)) req.Headers.TryAddWithoutValidation("Range", range.ToString());
        using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        Response.StatusCode = (int)resp.StatusCode;
        Response.ContentType = resp.Content.Headers.ContentType?.ToString() ?? "video/mp4";
        if (resp.Content.Headers.ContentLength is { } len) Response.ContentLength = len;
        if (resp.Content.Headers.ContentRange is { } cr) Response.Headers.ContentRange = cr.ToString();
        Response.Headers.AcceptRanges = "bytes";
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        await stream.CopyToAsync(Response.Body, ct);
        return new EmptyResult();
    }

    private LessonDto ToDto(Lesson l, int tier, Progress? p) => new(
        l.Id, l.ProgrammeId, l.Programme?.Name ?? "", l.Title, l.Description, l.Category, l.Level, l.DurationSeconds,
        l.ThumbnailUrl, l.IsPreview, !EntitlementService.CanWatch(l, tier, IsAdmin),
        p?.CompletionPercentage ?? 0, p?.WatchTimeSeconds ?? 0);
}
