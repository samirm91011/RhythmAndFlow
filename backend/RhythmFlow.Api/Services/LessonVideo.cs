using System.Text.RegularExpressions;

namespace RhythmFlow.Api.Services;

/// <summary>How a lesson's video is stored. A lesson is either a video file address (Remote or Local) or a YouTube video.</summary>
public static partial class LessonVideo
{
    public const string YouTube = "YouTube";
    /// <summary>A video uploaded to the private AWS bucket. The reference is its key in the bucket.</summary>
    public const string S3 = "S3";

    [GeneratedRegex(@"^[A-Za-z0-9_-]{11}$")]
    private static partial Regex IdPattern();

    /// <summary>
    /// Reads a YouTube video id from whatever the administrator pasted: a watch link, a youtu.be link, an embed or shorts link,
    /// or the bare 11-character id.
    /// </summary>
    public static bool TryGetYouTubeId(string? text, out string id)
    {
        id = "";
        var s = (text ?? "").Trim();
        if (s.Length == 0) return false;
        if (IdPattern().IsMatch(s)) { id = s; return true; }
        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return false;

        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.")) host = host[4..];
        if (host.StartsWith("m.")) host = host[2..];

        string? candidate = null;
        if (host == "youtu.be") candidate = uri.AbsolutePath.Trim('/').Split('/')[0];
        else if (host is "youtube.com" or "youtube-nocookie.com")
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2 && segments[0] is "embed" or "shorts" or "live" or "v") candidate = segments[1];
            else if (segments.Length == 1 && segments[0] == "watch")
                candidate = System.Web.HttpUtility.ParseQueryString(uri.Query)["v"];
        }
        if (candidate is not null && IdPattern().IsMatch(candidate)) { id = candidate; return true; }
        return false;
    }

    /// <summary>
    /// Checks the provider and reference an administrator entered. For YouTube the reference is stored as the bare video id,
    /// so any of the usual link shapes work. Returns the values to save, or a message to show.
    /// </summary>
    public static (bool Ok, string Provider, string Reference, string? Error) Normalise(string? provider, string? reference)
    {
        var p = (provider ?? "").Trim();
        var r = (reference ?? "").Trim();
        if (p.Equals(YouTube, StringComparison.OrdinalIgnoreCase))
            return TryGetYouTubeId(r, out var id)
                ? (true, YouTube, id, null)
                : (false, YouTube, r, "That doesn't look like a YouTube link. Paste the video's address, for example https://youtu.be/abcdefghijk.");
        if (p.Equals(S3, StringComparison.OrdinalIgnoreCase))
            return VideoUploads.IsValidKey(r)
                ? (true, S3, r, null)
                : (false, S3, r, "Upload the video first, then save the lesson.");
        return (true, string.IsNullOrEmpty(p) ? "Remote" : p, r, null);
    }

    public static bool IsS3(string? provider) => string.Equals(provider, S3, StringComparison.OrdinalIgnoreCase);

    public static bool IsYouTube(string? provider) => string.Equals(provider, YouTube, StringComparison.OrdinalIgnoreCase);
}
