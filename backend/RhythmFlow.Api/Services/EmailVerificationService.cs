using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;

namespace RhythmFlow.Api.Services;

/// <summary>
/// Confirms that a new customer can really read the mailbox they signed up with. A six-digit code is emailed; it expires after
/// 15 minutes, allows 5 guesses, and requests are limited per account. Until the code is entered the account cannot sign in.
/// </summary>
public class EmailVerificationService(AppDbContext db, IEmailSender email, IOptions<JwtOptions> jwt, ILogger<EmailVerificationService> log)
{
    private const string Purpose = "VERIFY";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
    private const int MaxAttempts = 5;
    private const int MaxRequestsPerHour = 3;
    /// <summary>Sign-ups that never confirm their email are removed after this many days, so nobody keeps a username by typing someone else's address.</summary>
    public const int UnverifiedAccountDays = 7;

    public const string Generic = "That code isn't valid or has expired. Please request a new one.";

    /// <summary>Emails a new code (replacing any earlier one). Returns false if too many were asked for in the last hour.</summary>
    public async Task<bool> SendCodeAsync(User user)
    {
        var since = DateTime.UtcNow.AddHours(-1);
        if (await db.PasswordResets.CountAsync(c => c.UserId == user.Id && c.Purpose == Purpose && c.CreatedAt > since) >= MaxRequestsPerHour)
        {
            log.LogWarning("Email verification rate limit hit for {UserId}", user.Id);
            return false;
        }
        foreach (var old in await db.PasswordResets.Where(c => c.UserId == user.Id && c.Purpose == Purpose && c.UsedAt == null).ToListAsync())
            old.UsedAt = DateTime.UtcNow;

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        db.PasswordResets.Add(new PasswordResetCode { UserId = user.Id, Purpose = Purpose, CodeHash = Hash(user.Id, code), ExpiresAt = DateTime.UtcNow.Add(Lifetime) });
        await db.SaveChangesAsync();

        await email.SendAsync(user.Email, "Confirm your email for Rhythm & Flow",
            $"Hi {user.FullName.Split(' ')[0]},\n\nWelcome to Rhythm & Flow. Your verification code is {code}.\n" +
            "Type it into the app to confirm your email address. It expires in 15 minutes.\n\n" +
            "If you didn't create an account, you can ignore this email.\n\nRhythm & Flow");
        return true;
    }

    /// <summary>Used when someone tries to sign in before verifying: sends a code only if they do not already have a live one.</summary>
    public async Task EnsureCodeAsync(User user)
    {
        var live = await db.PasswordResets.AnyAsync(c => c.UserId == user.Id && c.Purpose == Purpose && c.UsedAt == null && c.ExpiresAt > DateTime.UtcNow);
        if (!live) await SendCodeAsync(user);
    }

    /// <summary>Always completes the same way whether or not the email exists, so accounts cannot be discovered.</summary>
    public async Task ResendAsync(string emailAddress)
    {
        var address = emailAddress.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == address && u.AccountStatus == "ACTIVE" && !u.EmailVerified);
        if (user is not null) await SendCodeAsync(user);
    }

    public async Task<(bool Ok, string? Error)> VerifyAsync(string emailAddress, string code)
    {
        var address = emailAddress.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == address && u.AccountStatus == "ACTIVE");
        if (user is null) return (false, Generic);
        if (user.EmailVerified) return (true, null);   // already done (for example from another device): nothing to do

        var entry = await db.PasswordResets.Where(c => c.UserId == user.Id && c.Purpose == Purpose && c.UsedAt == null)
            .OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync();
        if (entry is null || entry.ExpiresAt < DateTime.UtcNow || entry.Attempts >= MaxAttempts) return (false, Generic);

        entry.Attempts++;
        var given = Encoding.ASCII.GetBytes(Hash(user.Id, code.Trim()));
        var expected = Encoding.ASCII.GetBytes(entry.CodeHash);
        if (!CryptographicOperations.FixedTimeEquals(given, expected))
        {
            await db.SaveChangesAsync();
            return (false, Generic);
        }

        entry.UsedAt = DateTime.UtcNow;
        user.EmailVerified = true;
        await db.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>Removes sign-ups that were never confirmed within a week, together with their codes.</summary>
    public async Task<int> PurgeUnverifiedAsync(DateTime now)
    {
        var cutoff = now.AddDays(-UnverifiedAccountDays);
        var stale = await db.Users.Where(u => !u.EmailVerified && u.Role == Roles.Customer && u.CreatedAt < cutoff).ToListAsync();
        if (stale.Count == 0) return 0;
        var ids = stale.Select(u => u.Id).ToList();
        db.PasswordResets.RemoveRange(db.PasswordResets.Where(c => ids.Contains(c.UserId)));
        db.Users.RemoveRange(stale);
        await db.SaveChangesAsync();
        return stale.Count;
    }

    private string Hash(Guid userId, string code)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(jwt.Value.Key));
        return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes($"{Purpose}|{userId}|{code}"))).ToLowerInvariant();
    }
}
