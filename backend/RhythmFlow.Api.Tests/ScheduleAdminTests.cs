using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

public class ScheduleAdminTests
{
    private static ScheduleAdminService Build(TestDb t) => new(t.Db, new NotificationService(t.Db));

    private static ClassUpsert Edit(ClassSession c, string? name = null, string? location = null, int? capacity = null, TimeSpan? shift = null, TimeSpan? length = null)
    {
        var start = c.StartTime + (shift ?? TimeSpan.Zero);
        return new ClassUpsert(name ?? c.Name, c.Description, c.CoachName, location ?? c.Location, start, start + (length ?? TimeSpan.FromHours(1)), capacity ?? c.Capacity);
    }

    private static void Book(TestDb t, User u, ClassSession c)
    {
        t.Db.Bookings.Add(new Booking { UserId = u.Id, ClassId = c.Id, Status = BookingStatus.Booked });
        t.Db.SaveChanges();
    }

    [Fact]
    public async Task Editing_a_class_saves_the_new_details()
    {
        using var t = new TestDb();
        var c = t.AddClass(10);

        var (ok, notFound, error) = await Build(t).UpdateClassAsync(c.Id, Edit(c, name: "Sunrise Flow", location: "Studio B", capacity: 14));

        Assert.True(ok); Assert.False(notFound); Assert.Null(error);
        var saved = await t.Db.Classes.AsNoTracking().SingleAsync(x => x.Id == c.Id);
        Assert.Equal("Sunrise Flow", saved.Name);
        Assert.Equal("Studio B", saved.Location);
        Assert.Equal(14, saved.Capacity);
    }

    [Fact]
    public async Task Capacity_cannot_drop_below_the_number_already_booked()
    {
        using var t = new TestDb();
        var c = t.AddClass(10);
        Book(t, t.AddUser("A"), c); Book(t, t.AddUser("B"), c); Book(t, t.AddUser("C"), c);

        var (ok, _, error) = await Build(t).UpdateClassAsync(c.Id, Edit(c, capacity: 2));
        var (okSame, _, _) = await Build(t).UpdateClassAsync(c.Id, Edit(c, capacity: 3));

        Assert.False(ok);
        Assert.Contains("3 people are already booked", error);
        Assert.True(okSame);
    }

    [Fact]
    public async Task End_before_start_is_refused()
    {
        using var t = new TestDb();
        var c = t.AddClass();

        var (ok, _, error) = await Build(t).UpdateClassAsync(c.Id, Edit(c, length: TimeSpan.FromMinutes(-5)));

        Assert.False(ok);
        Assert.Contains("End time must be after start time", error);
    }

    [Fact]
    public async Task A_cancelled_or_unknown_class_cannot_be_edited()
    {
        using var t = new TestDb();
        var c = t.AddClass();
        c.Status = "CANCELLED"; await t.Db.SaveChangesAsync();

        var (ok, _, error) = await Build(t).UpdateClassAsync(c.Id, Edit(c));
        var (_, notFound, _) = await Build(t).UpdateClassAsync(999, Edit(c));

        Assert.False(ok);
        Assert.Contains("cancelled class", error);
        Assert.True(notFound);
    }

    [Fact]
    public async Task People_booked_are_told_when_the_time_or_place_changes_but_not_for_other_edits()
    {
        using var t = new TestDb();
        var c = t.AddClass();
        var a = t.AddUser("A"); var b = t.AddUser("B"); var outsider = t.AddUser("Outsider");
        Book(t, a, c); Book(t, b, c);

        await Build(t).UpdateClassAsync(c.Id, Edit(c, name: "Renamed", capacity: 12));        // nothing moved
        Assert.Equal(0, await t.Db.Notifications.CountAsync());

        await Build(t).UpdateClassAsync(c.Id, Edit(c, shift: TimeSpan.FromHours(1)));           // time moved
        var told = await t.Db.Notifications.AsNoTracking().ToListAsync();
        Assert.Equal(2, told.Count);
        Assert.Equal(new[] { a.Id, b.Id }.OrderBy(x => x), told.Select(n => n.UserId).OrderBy(x => x));
        Assert.DoesNotContain(told, n => n.UserId == outsider.Id);
        Assert.All(told, n => Assert.Equal("Class updated", n.Title));
    }

    [Fact]
    public async Task A_lesson_edit_saves_changes_and_keeps_the_video_details_visible_to_admins_only()
    {
        using var t = new TestDb();
        var lesson = t.AddLesson();
        var programmeId = lesson.ProgrammeId;

        var (ok, _, error) = await Build(t).UpdateLessonAsync(lesson.Id,
            new LessonUpsert(programmeId, "New title", "New text", "Dance", "Beginner", 600, "Remote", "https://example.com/v.mp4", true));

        Assert.True(ok); Assert.Null(error);
        var row = Assert.Single(await Build(t).LessonsAsync());
        Assert.Equal("New title", row.Title);
        Assert.Equal("https://example.com/v.mp4", row.VideoReference);
        Assert.True(row.IsPreview);
        Assert.Equal("Beginner", row.Level);
    }

    [Fact]
    public async Task Moving_a_lesson_to_another_programme_puts_it_last_there_and_an_unknown_programme_is_refused()
    {
        using var t = new TestDb();
        var first = t.AddLesson();                 // programme A
        var other = t.AddLesson();                 // programme B
        var svc = Build(t);

        var (ok, _, _) = await svc.UpdateLessonAsync(first.Id, new LessonUpsert(other.ProgrammeId, "T", null, "Yoga", null, 100, "Remote", "https://x/y.mp4", false));
        var (bad, _, error) = await svc.UpdateLessonAsync(first.Id, new LessonUpsert(9999, "T", null, "Yoga", null, 100, "Remote", "https://x/y.mp4", false));

        Assert.True(ok);
        var moved = await t.Db.Lessons.AsNoTracking().SingleAsync(l => l.Id == first.Id);
        Assert.Equal(other.ProgrammeId, moved.ProgrammeId);
        Assert.True(moved.SequenceNumber > other.SequenceNumber);
        Assert.False(bad);
        Assert.Contains("Unknown programme", error);
    }

    [Fact]
    public async Task Editing_an_unknown_lesson_is_not_found()
    {
        using var t = new TestDb();
        var (_, notFound, _) = await Build(t).UpdateLessonAsync(999, new LessonUpsert(1, "T", null, "Yoga", null, 100, "Remote", "https://x/y.mp4", false));
        Assert.True(notFound);
    }
}
