using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;

namespace RhythmFlow.Api.Services;

public class NotificationService(AppDbContext db)
{
    public async Task AddAsync(Guid userId, string kind, string title, string body, string? route = null)
    {
        db.Notifications.Add(new AppNotification { UserId = userId, Kind = kind, Title = title, Body = body, Route = route });
        await db.SaveChangesAsync();
    }

    public async Task AddForAdminsAsync(string kind, string title, string body, string? route = null)
    {
        var admins = await db.Users.Where(u => u.Role == Roles.Admin && u.AccountStatus == "ACTIVE").Select(u => u.Id).ToListAsync();
        foreach (var id in admins)
            db.Notifications.Add(new AppNotification { UserId = id, Kind = kind, Title = title, Body = body, Route = route });
        await db.SaveChangesAsync();
    }

    /// <summary>Adds the same notification for several users without saving (call SaveChanges yourself).</summary>
    public void Stage(IEnumerable<Guid> userIds, string kind, string title, string body, string? route = null)
    {
        foreach (var id in userIds)
            db.Notifications.Add(new AppNotification { UserId = id, Kind = kind, Title = title, Body = body, Route = route });
    }
}
