using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;

namespace RhythmFlow.Api.Services;

public record ErrorReport(
    string Source, string Message, string? Details, string? Route, Guid? UserId, string? UserEmail,
    string? AppVersion, string? Device, string? FingerprintBasis = null);

/// <summary>
/// Records every error that reaches the API or is reported by the app. Identical errors are merged into one row with a
/// count. Administrators are notified (in the app and by email) when an error is new, comes back after being resolved,
/// or is still happening more than six hours after the last alert.
/// Logging must never throw, so every failure in here is swallowed after being written to the console log.
/// </summary>
public class ErrorLogService(IServiceScopeFactory scopes, IOptions<AdminOptions> admin, IEmailSender email, ILogger<ErrorLogService> log)
{
    private static readonly TimeSpan RenotifyAfter = TimeSpan.FromHours(6);

    public async Task<int?> LogAsync(ErrorReport r)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();

            var message = Trunc(r.Message, 500) ?? "";
            var fingerprint = Fingerprint(r.Source, r.FingerprintBasis ?? message);
            var now = DateTime.UtcNow;

            var row = await db.ErrorLogs.OrderByDescending(e => e.LastSeen).FirstOrDefaultAsync(e => e.Fingerprint == fingerprint);
            var notify = false;
            if (row is null)
            {
                row = new ErrorLog { Source = r.Source, Fingerprint = fingerprint, FirstSeen = now };
                db.ErrorLogs.Add(row);
                notify = true;
            }
            else
            {
                row.Count++;
                if (row.Status == "RESOLVED") { row.Status = "NEW"; notify = true; }
                else if (row.LastNotified is null || now - row.LastNotified > RenotifyAfter) notify = true;
            }
            row.Message = message;
            row.Details = Trunc(r.Details ?? "", 6000) ?? "";
            row.Route = Trunc(r.Route, 200);
            row.UserId = r.UserId ?? row.UserId;
            row.UserEmail = r.UserEmail ?? row.UserEmail;
            row.AppVersion = Trunc(r.AppVersion, 40);
            row.Device = Trunc(r.Device, 120);
            row.LastSeen = now;
            if (notify) row.LastNotified = now;
            await db.SaveChangesAsync();

            if (notify)
            {
                var title = r.Source == "APP" ? "App error reported" : "Server error";
                await notifications.AddForAdminsAsync("ADMIN_ERROR", title, Trunc(message, 140) ?? title, "admin/errors");
                var body = $"{title} (#{row.Id}, seen {row.Count}x)\n\n{message}\n\nRoute: {r.Route}\nUser: {r.UserEmail}\nApp: {r.AppVersion} {r.Device}\n\n{row.Details}";
                foreach (var to in admin.Value.AlertEmails.Where(a => !string.IsNullOrWhiteSpace(a)))
                    _ = email.SendAsync(to, $"[Rhythm & Flow] {title}: {Trunc(message, 80)}", body);
            }
            return row.Id;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to record an error report. Original: {Message}", r.Message);
            return null;
        }
    }

    private static string Fingerprint(string source, string basis)
    {
        // Strip numbers/ids so "booking 41 failed" and "booking 57 failed" count as the same problem.
        var normal = System.Text.RegularExpressions.Regex.Replace(basis.ToLowerInvariant(), @"[0-9a-f]{8}-[0-9a-f-]{27}|\d+", "#");
        return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(source + "|" + normal)))[..16];
    }

    private static string? Trunc(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];
}
