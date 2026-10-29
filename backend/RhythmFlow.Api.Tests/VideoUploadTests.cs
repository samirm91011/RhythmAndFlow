using RhythmFlow.Api.Domain;
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
        public List<string> Deleted { get; } = new();
        public bool FailDeletes { get; set; }
        public Task DeleteAsync(string key) { if (FailDeletes) throw new InvalidOperationException("AWS is down"); Deleted.Add(key); return Task.CompletedTask; }
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

    // ---- The file is removed from AWS when nothing uses it any more ----

    private static (ScheduleAdminService svc, FakeStorage storage, TestDb t) Cleanup(bool failDeletes = false)
    {
        var t = new TestDb(); var storage = new FakeStorage { FailDeletes = failDeletes };
        return (new ScheduleAdminService(t.Db, new NotificationService(t.Db), null, storage), storage, t);
    }

    private static Lesson S3Lesson(TestDb t, string key, int programmeId)
    {
        var l = new Lesson { ProgrammeId = programmeId, Title = "Uploaded", Level = "Beginner", DurationSeconds = 600, VideoProvider = "S3", VideoReference = key };
        t.Db.Lessons.Add(l); t.Db.SaveChanges(); return l;
    }

    [Fact]
    public async Task Deleting_a_lesson_also_deletes_its_uploaded_video_from_storage()
    {
        var (svc, storage, t) = Cleanup(); using var _ = t;
        var seed = t.AddLesson(); var l = S3Lesson(t, "videos/202610/aaa-flow.mp4", seed.ProgrammeId);

        Assert.True(await svc.DeleteLessonAsync(l.Id));

        Assert.Equal(["videos/202610/aaa-flow.mp4"], storage.Deleted);
        Assert.False(t.Db.Lessons.Any(x => x.Id == l.Id));
    }

    [Fact]
    public async Task A_file_that_another_lesson_still_uses_is_kept()
    {
        var (svc, storage, t) = Cleanup(); using var _ = t;
        var seed = t.AddLesson(); var a = S3Lesson(t, "videos/202610/shared.mp4", seed.ProgrammeId); S3Lesson(t, "videos/202610/shared.mp4", seed.ProgrammeId);

        await svc.DeleteLessonAsync(a.Id);

        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task YouTube_and_link_lessons_never_touch_storage_and_a_missing_lesson_is_reported()
    {
        var (svc, storage, t) = Cleanup(); using var _ = t;
        var plain = t.AddLesson();   // a "Remote" link lesson

        Assert.True(await svc.DeleteLessonAsync(plain.Id));
        Assert.False(await svc.DeleteLessonAsync(99999));
        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task Replacing_the_video_deletes_the_old_file_but_saving_other_changes_keeps_it()
    {
        var (svc, storage, t) = Cleanup(); using var _ = t;
        var seed = t.AddLesson(); var l = S3Lesson(t, "videos/202610/old.mp4", seed.ProgrammeId);
        LessonUpsert Edit(string provider, string reference, string title) => new(l.ProgrammeId, title, "d", "Yoga", "Beginner", 600, provider, reference, false);

        Assert.True((await svc.UpdateLessonAsync(l.Id, Edit("S3", "videos/202610/old.mp4", "New title"))).Ok);   // same file
        Assert.Empty(storage.Deleted);

        Assert.True((await svc.UpdateLessonAsync(l.Id, Edit("S3", "videos/202610/new.mp4", "New title"))).Ok);   // replaced
        Assert.Equal(["videos/202610/old.mp4"], storage.Deleted);

        Assert.True((await svc.UpdateLessonAsync(l.Id, Edit("YouTube", "NFixPh6y39U", "Now YouTube"))).Ok);     // switched away
        Assert.Equal(["videos/202610/old.mp4", "videos/202610/new.mp4"], storage.Deleted);
    }

    [Fact]
    public async Task If_AWS_cannot_delete_the_file_the_lesson_is_still_deleted()
    {
        var (svc, _, t) = Cleanup(failDeletes: true); using var _ = t;
        var seed = t.AddLesson(); var l = S3Lesson(t, "videos/202610/x.mp4", seed.ProgrammeId);

        Assert.True(await svc.DeleteLessonAsync(l.Id));

        Assert.False(t.Db.Lessons.Any(x => x.Id == l.Id));
    }
}