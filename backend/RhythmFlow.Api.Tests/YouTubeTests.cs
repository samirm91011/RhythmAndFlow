using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

/// <summary>The client's free preview videos are public YouTube videos (feedback of 20 Oct 2026).</summary>
public class YouTubeTests
{
    [Theory]
    [InlineData("NFixPh6y39U", "NFixPh6y39U")]
    [InlineData("https://www.youtube.com/watch?v=NFixPh6y39U", "NFixPh6y39U")]
    [InlineData("https://youtube.com/watch?v=NFixPh6y39U&t=30s", "NFixPh6y39U")]
    [InlineData("https://m.youtube.com/watch?v=NFixPh6y39U", "NFixPh6y39U")]
    [InlineData("https://youtu.be/NFixPh6y39U", "NFixPh6y39U")]
    [InlineData("https://youtu.be/NFixPh6y39U?si=abc", "NFixPh6y39U")]
    [InlineData("https://www.youtube.com/embed/NFixPh6y39U", "NFixPh6y39U")]
    [InlineData("https://www.youtube.com/shorts/NFixPh6y39U", "NFixPh6y39U")]
    [InlineData("  https://youtu.be/Z6zMtGnV_AI  ", "Z6zMtGnV_AI")]
    public void Every_usual_YouTube_link_shape_gives_the_video_id(string pasted, string expected)
    {
        Assert.True(LessonVideo.TryGetYouTubeId(pasted, out var id));
        Assert.Equal(expected, id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("https://example.com/watch?v=NFixPh6y39U")]
    [InlineData("https://www.youtube.com/@rhythmandflowwellness")]
    [InlineData("https://www.youtube.com/playlist?list=PLabcdefghijklmnop")]
    [InlineData("https://www.youtube.com/watch?v=short")]
    [InlineData("javascript:alert(1)")]
    public void Things_that_are_not_a_single_video_are_refused(string pasted)
    {
        Assert.False(LessonVideo.TryGetYouTubeId(pasted, out _));
    }

    [Fact]
    public void A_YouTube_lesson_is_saved_as_the_bare_id_and_other_lessons_are_left_alone()
    {
        var yt = LessonVideo.Normalise("YouTube", "https://youtu.be/NFixPh6y39U?si=x");
        Assert.True(yt.Ok); Assert.Equal("YouTube", yt.Provider); Assert.Equal("NFixPh6y39U", yt.Reference);

        var file = LessonVideo.Normalise("Remote", " https://cdn.example.com/a.mp4 ");
        Assert.True(file.Ok); Assert.Equal("Remote", file.Provider); Assert.Equal("https://cdn.example.com/a.mp4", file.Reference);

        var bad = LessonVideo.Normalise("YouTube", "not a link");
        Assert.False(bad.Ok); Assert.Contains("YouTube link", bad.Error);
    }

    [Fact]
    public void The_playback_answer_names_the_YouTube_video_youtubeId_because_that_is_what_the_apps_read()
    {
        // System.Text.Json would otherwise write "youTubeId", which the Android app (Gson) silently ignores.
        var json = System.Text.Json.JsonSerializer.Serialize(new PlaybackDto("", DateTime.UtcNow, 0, "NFixPh6y39U"), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Contains("\"youtubeId\":\"NFixPh6y39U\"", json);
        Assert.DoesNotContain("youTubeId", json);
    }

    [Fact]
    public async Task The_channels_nine_videos_are_added_once_as_free_previews_with_their_real_categories()
    {
        using var t = new TestDb();

        await SeedData.EnsureFreePracticesAsync(t.Db);
        await SeedData.EnsureFreePracticesAsync(t.Db);

        var programme = await t.Db.Programmes.Include(p => p.Lessons).SingleAsync(p => p.Name == SeedData.FreePracticesName);
        Assert.Equal(9, programme.Lessons.Count);
        Assert.All(programme.Lessons, l => { Assert.True(l.IsPreview); Assert.Equal("YouTube", l.VideoProvider); Assert.Equal(11, l.VideoReference.Length); });
        var expected = new[] { "Barre & Pilates", "Breathwork", "Meditation", "Stretch & Mobility", "Strength & Conditioning" };
        Assert.Equal(expected.OrderBy(x => x), programme.Lessons.Select(l => l.Category).Distinct().OrderBy(x => x));
        Assert.Equal(9, programme.Lessons.Select(l => l.VideoReference).Distinct().Count());
    }

    [Fact]
    public async Task Hiding_the_free_practices_or_removing_a_video_is_not_undone_on_the_next_start()
    {
        using var t = new TestDb();
        await SeedData.EnsureFreePracticesAsync(t.Db);
        var programme = await t.Db.Programmes.Include(p => p.Lessons).SingleAsync();
        programme.Status = "INACTIVE";
        t.Db.Lessons.Remove(programme.Lessons.First());
        await t.Db.SaveChangesAsync();

        await SeedData.EnsureFreePracticesAsync(t.Db);

        var after = await t.Db.Programmes.Include(p => p.Lessons).SingleAsync();
        Assert.Equal("INACTIVE", after.Status);
        Assert.Equal(8, after.Lessons.Count);
    }

    [Fact]
    public async Task An_administrator_can_switch_a_lesson_to_a_YouTube_video_and_a_bad_link_is_refused()
    {
        using var t = new TestDb();
        var svc = new ScheduleAdminService(t.Db, new NotificationService(t.Db));
        var lesson = t.AddLesson();
        LessonUpsert Body(string reference) =>
            new(lesson.ProgrammeId, "Title", "d", "Yoga", "All levels", 600, "YouTube", reference, true);

        var bad = await svc.UpdateLessonAsync(lesson.Id, Body("https://example.com/video"));
        Assert.False(bad.Ok); Assert.Contains("YouTube link", bad.Error);

        var good = await svc.UpdateLessonAsync(lesson.Id, Body("https://youtu.be/NFixPh6y39U"));
        Assert.True(good.Ok);
        var saved = await t.Db.Lessons.AsNoTracking().SingleAsync();
        Assert.Equal("YouTube", saved.VideoProvider); Assert.Equal("NFixPh6y39U", saved.VideoReference);
    }

    [Fact]
    public async Task A_YouTube_preview_can_be_watched_without_a_subscription_but_a_non_preview_one_still_needs_a_plan()
    {
        using var t = new TestDb();
        await SeedData.EnsureFreePracticesAsync(t.Db);
        var lesson = await t.Db.Lessons.Include(l => l.Programme).FirstAsync();
        Assert.True(EntitlementService.CanWatch(lesson, 0, false));

        lesson.IsPreview = false; lesson.Programme!.MinTier = 2;
        Assert.False(EntitlementService.CanWatch(lesson, 0, false));
        Assert.True(EntitlementService.CanWatch(lesson, 2, false));
    }
}
