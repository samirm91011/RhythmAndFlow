using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;

namespace RhythmFlow.Api.Services;

/// <summary>Lets administrators find customers and switch an account off or back on.</summary>
public class UserAdminService(AppDbContext db)
{
    public async Task<List<AdminUserDto>> ListAsync(string? search)
    {
        var q = db.Users.AsNoTracking().Where(u => u.AccountStatus != "DELETED");
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(u => u.Email.ToLower().Contains(s) || u.FullName.ToLower().Contains(s) || u.Username.ToLower().Contains(s));
        }
        var users = (await q.ToListAsync()).OrderByDescending(u => u.CreatedAt).Take(100).ToList();

        var now = DateTime.UtcNow;
        var ids = users.Select(u => u.Id).ToList();
        var live = await db.Subscriptions.AsNoTracking().Include(s => s.Plan)
            .Where(s => ids.Contains(s.UserId) && s.Status == SubscriptionStatus.Active && (s.EndDate == null || s.EndDate > now))
            .ToListAsync();
        var planByUser = live.GroupBy(s => s.UserId).ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.Plan!.Tier).First().Plan!.Name);

        return users.Select(u => new AdminUserDto(u.Id, u.FullName, u.Username, u.Email, u.Role, u.AccountStatus, u.CreatedAt,
            planByUser.GetValueOrDefault(u.Id))).ToList();
    }

    /// <summary>Switches an account between ACTIVE and DISABLED. A disabled person is signed out everywhere and cannot log in.</summary>
    public async Task<(bool Ok, bool NotFound, string? Error)> SetStatusAsync(Guid actingAdminId, Guid targetId, string status)
    {
        if (status is not ("ACTIVE" or "DISABLED")) return (false, false, "Status must be ACTIVE or DISABLED.");
        if (targetId == actingAdminId) return (false, false, "You can't change your own account here.");

        var user = await db.Users.FindAsync(targetId);
        if (user is null || user.AccountStatus == "DELETED") return (false, true, null);

        if (status == "DISABLED" && user.Role == Roles.Admin &&
            !await db.Users.AnyAsync(u => u.Role == Roles.Admin && u.AccountStatus == "ACTIVE" && u.Id != targetId))
            return (false, false, "That is the only active administrator.");

        user.AccountStatus = status;
        user.SecurityStamp = Guid.NewGuid().ToString("N");   // invalidates every login token they hold
        await db.SaveChangesAsync();
        return (true, false, null);
    }
}
