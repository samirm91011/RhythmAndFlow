using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Controllers;

/// <summary>In-app notifications. The app polls this and also shows new items as phone notifications.</summary>
[ApiController, Authorize]
[Route("api/notifications")]
public class NotificationsController(AppDbContext db, NotificationService notifications) : ApiController
{
    /// <summary>Registers this phone for push notifications (called after sign-in and whenever Firebase issues a new token).</summary>
    [HttpPost("device-token"), EnableRateLimiting("auth")]
    public async Task<IActionResult> RegisterDevice(DeviceTokenRequest req)
    {
        await notifications.RegisterTokenAsync(UserId, req.Token.Trim());
        return Ok(new { message = "Registered." });
    }

    /// <summary>Stops push notifications to this phone (called on log out).</summary>
    [HttpPost("device-token/remove"), EnableRateLimiting("auth")]
    public async Task<IActionResult> RemoveDevice(DeviceTokenRequest req)
    {
        await notifications.RemoveTokenAsync(UserId, req.Token.Trim());
        return Ok(new { message = "Removed." });
    }

    [HttpGet]
    public async Task<List<NotificationDto>> List([FromQuery] int? afterId, [FromQuery] bool unreadOnly = false)
    {
        var q = db.Notifications.Where(n => n.UserId == UserId);
        if (afterId is not null) q = q.Where(n => n.Id > afterId);
        if (unreadOnly) q = q.Where(n => n.ReadAt == null);
        var rows = await q.OrderByDescending(n => n.Id).Take(100).ToListAsync();
        return rows.Select(n => new NotificationDto(n.Id, n.Kind, n.Title, n.Body, n.Route, n.CreatedAt, n.ReadAt != null)).ToList();
    }

    [HttpGet("unread-count")]
    public async Task<object> UnreadCount() => new { count = await db.Notifications.CountAsync(n => n.UserId == UserId && n.ReadAt == null) };

    /// <summary>Marks the given notifications read, or all of them when no ids are sent.</summary>
    [HttpPost("read")]
    public async Task<IActionResult> MarkRead(MarkReadRequest req)
    {
        var q = db.Notifications.Where(n => n.UserId == UserId && n.ReadAt == null);
        if (req.Ids is { Count: > 0 }) q = q.Where(n => req.Ids.Contains(n.Id));
        foreach (var n in await q.ToListAsync()) n.ReadAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }
}

/// <summary>Receives error reports from the app so administrators hear about problems customers hit.</summary>
[ApiController]
[Route("api/telemetry")]
public class TelemetryController(ErrorLogService errors) : ControllerBase
{
    [AllowAnonymous, EnableRateLimiting("telemetry"), HttpPost("errors")]
    public async Task<IActionResult> Report(ErrorReportRequest req)
    {
        var userId = Guid.TryParse(User.FindFirstValue("sub"), out var u) ? u : (Guid?)null;
        var id = await errors.LogAsync(new ErrorReport(
            "APP", (req.Fatal ? "[CRASH] " : "") + req.Message, req.Details, req.Route, userId, User.FindFirstValue("email"),
            req.AppVersion, req.Device, FingerprintBasis: req.Message + "|" + req.Details?.Split('\n').FirstOrDefault()));
        return Ok(new { referenceId = id });
    }
}
