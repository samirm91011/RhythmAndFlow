using System.Text.RegularExpressions;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using RhythmFlow.Api.Dtos;

namespace RhythmFlow.Api.Services;

/// <summary>
/// Settings for the private video bucket on AWS (section "Video" of the configuration). Leave Bucket empty to switch video uploads off.
/// If AccessKey and SecretKey are empty the server uses the AWS identity of the machine it runs on (an IAM role or the usual AWS_* settings).
/// </summary>
public class VideoStorageOptions
{
    public string Bucket { get; set; } = "";
    /// <summary>Cape Town is af-south-1.</summary>
    public string Region { get; set; } = "af-south-1";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    /// <summary>Only for testing with a stand-in for S3 on this computer. Leave empty on a real server.</summary>
    public string ServiceUrl { get; set; } = "";
    /// <summary>How long an upload link works. Long, because a big video on a slow connection takes time.</summary>
    public int UploadMinutes { get; set; } = 180;
    /// <summary>How long a playback link works. Members get a fresh one each time they press play.</summary>
    public int PlaybackMinutes { get; set; } = 120;
    /// <summary>The biggest video that can be uploaded, in gigabytes.</summary>
    public int MaxUploadGb { get; set; } = 5;
}

/// <summary>Private video storage. Videos are never public: both uploading and watching use short-lived signed links.</summary>
public interface IVideoStorage
{
    bool Configured { get; }
    long MaxUploadBytes { get; }
    TimeSpan UploadValidFor { get; }
    TimeSpan PlaybackValidFor { get; }
    string SignUpload(string key, string contentType);
    string SignPlayback(string key);
}

/// <summary>Used until a bucket is set up: the app says uploads are not available yet.</summary>
public class NoVideoStorage : IVideoStorage
{
    public bool Configured => false;
    public long MaxUploadBytes => 0;
    public TimeSpan UploadValidFor => TimeSpan.Zero;
    public TimeSpan PlaybackValidFor => TimeSpan.Zero;
    public string SignUpload(string key, string contentType) => throw new InvalidOperationException("Video storage is not set up.");
    public string SignPlayback(string key) => throw new InvalidOperationException("Video storage is not set up.");
}

/// <summary>Amazon S3. Signing a link is done on this server with no call to AWS, so it is instant and free.</summary>
public sealed class S3VideoStorage : IVideoStorage
{
    private readonly VideoStorageOptions _o;
    private readonly AmazonS3Client _s3;

    public S3VideoStorage(IOptions<VideoStorageOptions> options)
    {
        _o = options.Value;
        var config = new AmazonS3Config { RegionEndpoint = RegionEndpoint.GetBySystemName(_o.Region) };
        if (!string.IsNullOrWhiteSpace(_o.ServiceUrl)) { config.ServiceURL = _o.ServiceUrl.Trim(); config.ForcePathStyle = true; }
        _s3 = string.IsNullOrWhiteSpace(_o.AccessKey)
            ? new AmazonS3Client(config)
            : new AmazonS3Client(new BasicAWSCredentials(_o.AccessKey.Trim(), _o.SecretKey.Trim()), config);
    }

    private Protocol Scheme => _o.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? Protocol.HTTP : Protocol.HTTPS;

    public bool Configured => true;
    public long MaxUploadBytes => Math.Max(1, _o.MaxUploadGb) * 1024L * 1024 * 1024;
    public TimeSpan UploadValidFor => TimeSpan.FromMinutes(Math.Max(10, _o.UploadMinutes));
    public TimeSpan PlaybackValidFor => TimeSpan.FromMinutes(Math.Max(10, _o.PlaybackMinutes));

    public string SignUpload(string key, string contentType) => _s3.GetPreSignedURL(new GetPreSignedUrlRequest
    {
        BucketName = _o.Bucket, Key = key, Verb = HttpVerb.PUT, ContentType = contentType, Expires = DateTime.UtcNow.Add(UploadValidFor), Protocol = Scheme,
    });

    public string SignPlayback(string key) => _s3.GetPreSignedURL(new GetPreSignedUrlRequest
    {
        BucketName = _o.Bucket, Key = key, Verb = HttpVerb.GET, Expires = DateTime.UtcNow.Add(PlaybackValidFor), Protocol = Scheme,
    });
}

/// <summary>The rules for an administrator uploading a video: what is accepted, and where it is stored.</summary>
public static partial class VideoUploads
{
    public const string KeyPrefix = "videos/";

    private static readonly string[] AllowedTypes = ["video/mp4", "video/quicktime", "video/webm", "video/x-matroska", "video/x-m4v", "video/mpeg"];

    [GeneratedRegex(@"[^A-Za-z0-9._-]+")]
    private static partial Regex Unsafe();

    /// <summary>A stored video's key must be one of ours: it starts with "videos/" and cannot climb out with "..".</summary>
    [GeneratedRegex(@"^videos/[A-Za-z0-9/_.-]{3,300}$")]
    private static partial Regex KeyPattern();

    public static bool IsValidKey(string? key) => key is not null && KeyPattern().IsMatch(key) && !key.Contains("..") && !key.Contains("//");

    public static (VideoUploadResponse? Result, string? Error) Create(IVideoStorage storage, VideoUploadRequest r)
    {
        if (!storage.Configured)
            return (null, "Video uploads are not set up yet. Ask the developer to connect the video storage.");
        var type = (r.ContentType ?? "").Trim().ToLowerInvariant();
        if (type == "") type = "video/mp4";
        if (!AllowedTypes.Contains(type)) return (null, "Please choose a video file (MP4 or MOV is best).");
        if (r.SizeBytes <= 0) return (null, "That file looks empty.");
        if (r.SizeBytes > storage.MaxUploadBytes) return (null, $"That video is too big. The limit is {storage.MaxUploadBytes / (1024 * 1024 * 1024)} GB. Please compress it first.");

        var name = Unsafe().Replace(Path.GetFileName(r.FileName ?? "video.mp4"), "-").Trim('-', '.');
        if (name.Length == 0) name = "video.mp4";
        if (name.Length > 80) name = name[^80..];
        var key = $"{KeyPrefix}{DateTime.UtcNow:yyyyMM}/{Guid.NewGuid():N}-{name}";
        var url = storage.SignUpload(key, type);
        return (new VideoUploadResponse(key, url, type, DateTime.UtcNow.Add(storage.UploadValidFor)), null);
    }
}
