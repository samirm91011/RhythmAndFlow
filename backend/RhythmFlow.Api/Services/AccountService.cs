using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;

namespace RhythmFlow.Api.Services;

/// <summary>
/// Lets a person take a copy of their data and delete their account (POPIA: access and deletion rights).
/// Personal content is erased. Subscription and payment rows are kept without any personal details, because
/// the business has to keep financial records.
/// </summary>
public class AccountService(AppDbContext db, SubscriptionService subs)
{
    private static readonly PasswordHasher<User> Hasher = new();

    public async Task<object?> ExportAsync(Guid userId)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return null;

        var subscriptions = await db.Subscriptions.AsNoTracking().Include(s => s.Plan)
            .Where(s => s.UserId == userId).OrderBy(s => s.CreatedAt).ToListAsync();
        var subIds = subscriptions.Select(s => s.Id).ToList();
        var payments = await db.Payments.AsNoTracking().Where(p => subIds.Contains(p.SubscriptionId)).ToListAsync();
        var progress = await db.Progress.AsNoTracking().Include(p => p.Lesson).Where(p => p.UserId == userId).ToListAsync();
        var bookings = await db.Bookings.AsNoTracking().Include(b => b.Class).Where(b => b.UserId == userId).ToListAsync();
        var journal = await db.Journal.AsNoTracking().Where(j => j.UserId == userId).ToListAsync();
        var notifications = await db.Notifications.AsNoTracking().Where(n => n.UserId == userId).ToListAsync();

        return new
        {
            exportedAt = DateTime.UtcNow,
            note = "This is a copy of the personal information Rhythm & Flow holds about you.",
            profile = new { user.FullName, user.Username, user.Email, user.About, user.Role, user.CreatedAt },
            subscriptions = subscriptions.Select(s => new
            {
                plan = s.Plan?.Name, s.Status, s.StartDate, s.EndDate, s.CreatedAt,
            }),
            payments = payments.OrderBy(p => p.PaymentDate).Select(p => new { p.Amount, p.Status, p.PaymentDate }),
            progress = progress.Select(p => new
            {
                lesson = p.Lesson?.Title, p.WatchTimeSeconds, p.CompletionPercentage, p.Completed, p.LastWatchedAt,
            }),
            bookings = bookings.Select(b => new { @class = b.Class?.Name, startTime = b.Class?.StartTime, b.Status, b.BookingDate }),
            journal = journal.OrderBy(j => j.CreatedAt).Select(j => new { j.Kind, j.Mood, j.Text, j.CreatedAt }),
            notifications = notifications.OrderBy(n => n.CreatedAt).Select(n => new { n.Kind, n.Title, n.Body, n.CreatedAt }),
        };
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid userId, string password)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null || user.AccountStatus != "ACTIVE") return (false, "Account not found.");
        if (Hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
            return (false, "That password isn't right.");

        if (user.Role == Roles.Admin &&
            !await db.Users.AnyAsync(u => u.Role == Roles.Admin && u.AccountStatus == "ACTIVE" && u.Id != userId))
            return (false, "You are the only administrator, so this account can't be deleted. Make someone else an administrator first.");

        // Stop future PayFast charges before anything is removed. If PayFast refuses, nothing is deleted.
        var active = await db.Subscriptions.Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active).Select(s => s.Id).ToListAsync();
        foreach (var id in active)
        {
            var (ok, _, error) = await subs.CancelAsync(userId, id);
            if (!ok) return (false, "We couldn't cancel your subscription with PayFast, so your account has not been deleted. " + error);
        }

        db.Journal.RemoveRange(db.Journal.Where(j => j.UserId == userId));
        db.Progress.RemoveRange(db.Progress.Where(p => p.UserId == userId));
        db.Bookings.RemoveRange(db.Bookings.Where(b => b.UserId == userId));
        db.Notifications.RemoveRange(db.Notifications.Where(n => n.UserId == userId));
        db.PasswordResets.RemoveRange(db.PasswordResets.Where(r => r.UserId == userId));
        db.DeviceTokens.RemoveRange(db.DeviceTokens.Where(t => t.UserId == userId));
        foreach (var log in await db.ErrorLogs.Where(l => l.UserId == userId).ToListAsync())
        {
            log.UserId = null;
            log.UserEmail = null;
        }

        if (await db.Subscriptions.AnyAsync(s => s.UserId == userId))
        {
            // Keep the financial records, but nothing that identifies the person.
            var tag = userId.ToString("N");
            user.FullName = "Deleted user";
            user.Username = "deleted-" + tag[..12];
            user.Email = $"deleted-{tag}@deleted.invalid";
            user.About = "";
            user.PasswordHash = "";
            user.AccountStatus = "DELETED";
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }
        else
        {
            db.Users.Remove(user);
        }

        await db.SaveChangesAsync();
        return (true, null);
    }
}
