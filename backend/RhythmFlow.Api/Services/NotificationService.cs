using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;

namespace RhythmFlow.Api.Services;

/// <summary>
/// Creates in-app notifications and, when Firebase is configured, pushes each one to the person's phones.
/// A failed push never fails the request that caused it: the in-app notification is saved first, and the
/// app's periodic sync picks it up anyway.
/// </summary>
public class NotificationService(AppDbContext db, IPushSender? push = null, ILogger<NotificationService>? log = null)
{
    private const int MaxPhonesPerUser = 5;
    private readonly List<AppNotification> _staged = new();

    public async Task AddAsync(Guid userId, string kind, string title, string body, string? route = null)
    {
        var n = new AppNotification { UserId = userId, Kind = kind, Title = title, Body = body, Route = route };
        db.Notifications.Add(n);
        await db.SaveChangesAsync();
        await PushAsync(n);
    }

    public async Task AddForAdminsAsync(string kind, string title, string body, string? route = null)
    {
        var admins = await db.Users.Where(u => u.Role == Roles.Admin && u.AccountStatus == "ACTIVE").Select(u => u.Id).ToListAsync();
        var created = admins.Select(id => new AppNotification { UserId = id, Kind = kind, Title = title, Body = body, Route = route }).ToList();
        db.Notifications.AddRange(created);
        await db.SaveChangesAsync();
        foreach (var n in created) await PushAsync(n);
    }

    /// <summary>Adds the same notification for several users without saving (call SaveChanges, then <see cref="PushStagedAsync"/>).</summary>
    public void Stage(IEnumerable<Guid> userIds, string kind, string title, string body, string? route = null)
    {
        foreach (var id in userIds)
        {
            var n = new AppNotification { UserId = id, Kind = kind, Title = title, Body = body, Route = route };
            db.Notifications.Add(n);
            _staged.Add(n);
        }
    }

    /// <summary>Pushes the notifications added with <see cref="Stage"/>. Call after they have been saved.</summary>
    public async Task PushStagedAsync()
    {
        var list = _staged.ToList();
        _staged.Clear();
        foreach (var n in list) await PushAsync(n);
    }

    /// <summary>Remembers a phone for push. A phone that signs in as someone else moves to the new person.</summary>
    public async Task RegisterTokenAsync(Guid userId, string token)
    {
        var row = await db.DeviceTokens.FirstOrDefaultAsync(t => t.Token == token);
        if (row is null) db.DeviceTokens.Add(new DeviceToken { UserId = userId, Token = token });
        else { row.UserId = userId; row.LastSeen = DateTime.UtcNow; }
        await db.SaveChangesAsync();

        // Keep only the most recently used phones so an old or shared account cannot grow this list without limit.
        var surplus = await db.DeviceTokens.Where(t => t.UserId == userId).OrderByDescending(t => t.LastSeen).Skip(MaxPhonesPerUser).ToListAsync();
        if (surplus.Count > 0)
        {
            db.DeviceTokens.RemoveRange(surplus);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Forgets a phone (called when the person logs out, so it stops receiving their notifications).</summary>
    public async Task RemoveTokenAsync(Guid userId, string token)
    {
        var rows = await db.DeviceTokens.Where(t => t.UserId == userId && t.Token == token).ToListAsync();
        if (rows.Count == 0) return;
        db.DeviceTokens.RemoveRange(rows);
        await db.SaveChangesAsync();
    }

    private async Task PushAsync(AppNotification n)
    {
        if (push is null || !push.Enabled) return;
        try
        {
            var tokens = await db.DeviceTokens.Where(t => t.UserId == n.UserId).Select(t => t.Token).ToListAsync();
            if (tokens.Count == 0) return;
            var invalid = await push.SendAsync(tokens, new PushMessage(n.Title, n.Body, n.Kind, n.Route, n.Id));
            if (invalid.Count > 0)
            {
                db.DeviceTokens.RemoveRange(db.DeviceTokens.Where(t => invalid.Contains(t.Token)));
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            log?.LogWarning(ex, "Push notification failed; the in-app notification was still saved.");
        }
    }
}
