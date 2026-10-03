using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;

namespace RhythmFlow.Api.Services;

/// <summary>Six-digit emailed codes. Codes expire after 15 minutes, allow 5 guesses, and requests are limited per account.</summary>
public class PasswordResetService(AppDbContext db, IEmailSender email, IOptions<JwtOptions> jwt, ILogger<PasswordResetService> log)
{
    private static readonly PasswordHasher<User> Hasher = new();
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
    private const int MaxAttempts = 5;
    private const int MaxRequestsPerHour = 3;

    /// <summary>Always completes the same way whether or not the email exists, so accounts cannot be discovered.</summary>
    public async Task RequestAsync(string emailAddress)
    {
        var address = emailAddress.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == address && u.AccountStatus == "ACTIVE");
        if (user is null) return;

        var since = DateTime.UtcNow.AddHours(-1);
        if (await db.PasswordResets.CountAsync(c => c.UserId == user.Id && c.CreatedAt > since) >= MaxRequestsPerHour)
        {
            log.LogWarning("Password reset rate limit hit for {UserId}", user.Id);
            return;
        }

        // Only the newest code works.
        foreach (var old in await db.PasswordResets.Where(c => c.UserId == user.Id && c.UsedAt == null).ToListAsync())
            old.UsedAt = DateTime.UtcNow;

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        db.PasswordResets.Add(new PasswordResetCode { UserId = user.Id, CodeHash = Hash(user.Id, code), ExpiresAt = DateTime.UtcNow.Add(Lifetime) });
        await db.SaveChangesAsync();

        await email.SendAsync(user.Email, "Your Rhythm & Flow password reset code",
            $"Hi {user.FullName.Split(' ')[0]},\n\nYour password reset code is {code}.\nIt expires in 15 minutes. If you didn't ask for this, you can ignore this email.\n\nRhythm & Flow");
    }

    public async Task<(bool Ok, string? Error)> ResetAsync(string emailAddress, string code, string newPassword)
    {
        const string generic = "That code isn't valid or has expired. Please request a new one.";
        var address = emailAddress.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == address && u.AccountStatus == "ACTIVE");
        if (user is null) return (false, generic);

        var entry = await db.PasswordResets.Where(c => c.UserId == user.Id && c.UsedAt == null)
            .OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync();
        if (entry is null || entry.ExpiresAt < DateTime.UtcNow || entry.Attempts >= MaxAttempts) return (false, generic);

        entry.Attempts++;
        var given = Encoding.ASCII.GetBytes(Hash(user.Id, code.Trim()));
        var expected = Encoding.ASCII.GetBytes(entry.CodeHash);
        if (!CryptographicOperations.FixedTimeEquals(given, expected))
        {
            await db.SaveChangesAsync();
            return (false, generic);
        }

        entry.UsedAt = DateTime.UtcNow;
        user.PasswordHash = Hasher.HashPassword(user, newPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N"); // signs the account out everywhere else
        await db.SaveChangesAsync();

        _ = email.SendAsync(user.Email, "Your Rhythm & Flow password was changed",
            "Your password was just changed. If this wasn't you, please contact Rhythm & Flow straight away.");
        return (true, null);
    }

    private string Hash(Guid userId, string code)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(jwt.Value.Key));
        return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes($"{userId}|{code}"))).ToLowerInvariant();
    }
}
