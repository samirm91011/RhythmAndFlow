using System.Security.Cryptography;
using System.Text;

namespace RhythmFlow.Api.Services;

public class MediaOptions
{
    /// <summary>Secret used to sign playback links. Must be long and random in production.</summary>
    public string SigningKey { get; set; } = "";
    public int LinkMinutes { get; set; } = 60;
    /// <summary>Folder for videos with VideoProvider "Local".</summary>
    public string LocalFolder { get; set; } = "media";
}

/// <summary>
/// Creates short-lived, per-user signed playback links so a copied link stops working quickly
/// and the original video location is never exposed to the app.
/// </summary>
public class MediaTokenService(Microsoft.Extensions.Options.IOptions<MediaOptions> options)
{
    private readonly MediaOptions _o = options.Value;

    public (string Query, DateTime ExpiresAt) Sign(int lessonId, Guid userId)
    {
        var expires = DateTime.UtcNow.AddMinutes(_o.LinkMinutes);
        var exp = new DateTimeOffset(expires).ToUnixTimeSeconds();
        var sig = Compute(lessonId, userId, exp);
        return ($"u={userId}&e={exp}&s={sig}", expires);
    }

    public bool Validate(int lessonId, Guid userId, long exp, string sig)
    {
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) return false;
        var expected = Encoding.ASCII.GetBytes(Compute(lessonId, userId, exp));
        var given = Encoding.ASCII.GetBytes(sig ?? "");
        return CryptographicOperations.FixedTimeEquals(expected, given);
    }

    private string Compute(int lessonId, Guid userId, long exp)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(_o.SigningKey));
        var data = Encoding.UTF8.GetBytes($"{lessonId}|{userId}|{exp}");
        return Convert.ToHexString(h.ComputeHash(data)).ToLowerInvariant();
    }
}
