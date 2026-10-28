using RhythmFlow.Api.Dtos;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

/// <summary>Uploading a video from the admin screen to the private AWS bucket, and saving a lesson that points at it.</summary>
public class VideoUploadTests
{
    private sealed class FakeStorage(bool configured = true, long max = 5L * 1024 * 1024 * 1024) : IVideoStorage
    {
        public bool Configured => configured;
        public long MaxUploadBytes => max;
        public TimeSpan UploadValidFor => TimeSpan.FromMinutes(180);
        public TimeSpan PlaybackValidFor => TimeSpan.FromMinutes(120);
        public string SignUpload(string key, string contentType) => $"https://bucket.example/{key}?upload&type={contentType}";
        public string SignPlayback(string key) => $"https://bucket.example/{key}?play";
    }

    [Fact]
    public void A_video_gets_a_private_key_under_videos_and_a_signed_upload_link()
    {
        var (result, error) = VideoUploads.Create(new FakeStorage(), new VideoUploadRequest("Morning Flow 10 min.mp4", "video/mp4", 400_000_000));

        Assert.Null(error);
        Assert.StartsWith("videos/", result!.Key);
        Assert.EndsWith("Morning-Flow-10-min.mp4", result.Key);
        Assert.Contains(result.Key, result.UploadUrl);
        Assert.Equal("video/mp4", result.ContentType);
        Assert.True(VideoUploads.IsValidKey(result.Key));
    }

    [Fact]
    public void Two_uploads_of_the_same_file_name_never_share_a_key()
    {
        var r = new VideoUploadRequest("flow.mp4", "video/mp4", 1000);
        Assert.NotEqual(VideoUploads.Create(new FakeStorage(), r).Result!.Key, VideoUploads.Create(new FakeStorage(), r).Result!.Key);
    }

    [Theory]
    [InlineData("../../etc/passwd", "video/mp4")]
    [InlineData("a b/c\\d.mp4", "video/mp4")]
    public void A_file_name_cannot_climb_out_of_the_videos_folder(string name, string type)
    {
        var key = VideoUploads.Create(new FakeStorage(), new VideoUploadRequest(name, type, 1000)).Result!.Key;
        Assert.True(VideoUploads.IsValidKey(key));
        Assert.DoesNotContain("..", key);
    }

    [Fact]
    public void Only_video_files_of_a_sensible_size_are_accepted()
    {
        var storage = new FakeStorage(max: 1000);
        Assert.Contains("choose a video", VideoUploads.Create(storage, new VideoUploadRequest("a.pdf", "application/pdf", 500)).Error);
        Assert.Contains("empty", VideoUploads.Create(storage, new VideoUploadRequest("a.mp4", "video/mp4", 0)).Error);
        Assert.Contains("too big", VideoUploads.Create(storage, new VideoUploadRequest("a.mp4", "video/mp4", 5000)).Error);
        Assert.Null(VideoUploads.Create(storage, new VideoUploadRequest("a.mp4", "video/quicktime", 999)).Error);   // an iPhone recording
    }

    [Fact]
    public void Without_a_bucket_the_admin_is_told_uploads_are_not_set_up_yet()
    {
        var (result, error) = VideoUploads.Create(new NoVideoStorage(), new VideoUploadRequest("a.mp4", "video/mp4", 1000));
        Assert.Null(result); Assert.Contains("not set up yet", error);
    }

    [Theory]
    [InlineData("videos/202610/abc-flow.mp4", true)]
    [InlineData("videos/../secret", false)]
    [InlineData("other/abc.mp4", false)]
    [InlineData("videos//abc.mp4", false)]
    [InlineData("https://evil.example/a.mp4", false)]
    [InlineData("", false)]
    public void Only_our_own_video_keys_can_be_saved_on_a_lesson(string key, bool ok)
    {
        var r = LessonVideo.Normalise("S3", key);
        Assert.Equal(ok, r.Ok);
        if (ok) { Assert.Equal("S3", r.Provider); Assert.Equal(key, r.Reference); }
        else Assert.Contains("Upload the video first", r.Error);
    }

    [Fact]
    public void Existing_video_providers_still_work_as_before()
    {
        Assert.True(LessonVideo.Normalise("Remote", "https://cdn.example.com/a.mp4").Ok);
        Assert.True(LessonVideo.Normalise(null, "https://cdn.example.com/a.mp4").Ok);
        Assert.True(LessonVideo.IsS3("s3")); Assert.False(LessonVideo.IsS3("Remote"));
    }

    [Fact]
    public void The_real_AWS_storage_signs_upload_and_playback_links_for_the_bucket_without_calling_AWS()
    {
        var storage = new S3VideoStorage(Microsoft.Extensions.Options.Options.Create(new VideoStorageOptions { Bucket = "rhythmflow-videos", Region = "af-south-1", AccessKey = "AKIATEST", SecretKey = "secret" }));
        var up = new Uri(storage.SignUpload("videos/202610/a-flow.mp4", "video/mp4"));
        var play = new Uri(storage.SignPlayback("videos/202610/a-flow.mp4"));

        Assert.Equal("https", up.Scheme); Assert.Contains("rhythmflow-videos", up.Host); Assert.Contains("af-south-1", up.Host);
        Assert.Contains("X-Amz-Signature=", up.Query); Assert.Contains("X-Amz-Signature=", play.Query);
        Assert.Contains("a-flow.mp4", up.AbsolutePath); Assert.Contains("a-flow.mp4", play.AbsolutePath);
        Assert.True(storage.Configured);
    }
}