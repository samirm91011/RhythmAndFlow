using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

public class ProgrammeAdminTests
{
    private static ProgrammeUpsert Programme(string name = "Strength Foundations", int tier = 2, bool active = true, string? desc = "Build strength safely.") =>
        new(name, desc, tier, active);

    [Fact]
    public async Task A_new_programme_is_created_active_and_listed()
    {
        using var t = new TestDb();
        var svc = new ProgrammeAdminService(t.Db);

        var (ok, id, error) = await svc.CreateAsync(Programme());

        Assert.True(ok); Assert.Null(error); Assert.True(id > 0);
        var list = await svc.ListAsync();
        var row = Assert.Single(list);
        Assert.Equal("Strength Foundations", row.Name);
        Assert.Equal(2, row.MinTier);
        Assert.True(row.Active);
        Assert.Equal(0, row.LessonCount);
    }

    [Fact]
    public async Task Names_are_trimmed_and_must_be_unique_ignoring_case()
    {
        using var t = new TestDb();
        var svc = new ProgrammeAdminService(t.Db);
        await svc.CreateAsync(Programme("Move & Release"));

        var (ok, _, error) = await svc.CreateAsync(Programme("  move & release  "));

        Assert.False(ok);
        Assert.Contains("already a programme", error);
        Assert.Single(await svc.ListAsync());
    }

    [Fact]
    public async Task A_blank_name_is_refused()
    {
        using var t = new TestDb();
        var (ok, _, error) = await new ProgrammeAdminService(t.Db).CreateAsync(Programme("   "));
        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public async Task Editing_changes_name_description_level_and_visibility()
    {
        using var t = new TestDb();
        var svc = new ProgrammeAdminService(t.Db);
        var (_, id, _) = await svc.CreateAsync(Programme());

        var (ok, notFound, error) = await svc.UpdateAsync(id, new ProgrammeUpsert("Strength Plus", "More weights.", 3, false));

        Assert.True(ok); Assert.False(notFound); Assert.Null(error);
        var row = Assert.Single(await svc.ListAsync());
        Assert.Equal("Strength Plus", row.Name);
        Assert.Equal("More weights.", row.Description);
        Assert.Equal(3, row.MinTier);
        Assert.False(row.Active);
    }

    [Fact]
    public async Task A_programme_can_keep_its_own_name_when_edited_but_not_take_anothers()
    {
        using var t = new TestDb();
        var svc = new ProgrammeAdminService(t.Db);
        var (_, a, _) = await svc.CreateAsync(Programme("Alpha"));
        await svc.CreateAsync(Programme("Beta"));

        Assert.True((await svc.UpdateAsync(a, Programme("Alpha", tier: 3))).Ok);
        var (ok, _, error) = await svc.UpdateAsync(a, Programme("beta"));

        Assert.False(ok);
        Assert.Contains("already a programme", error);
    }

    [Fact]
    public async Task Editing_an_unknown_programme_reports_not_found()
    {
        using var t = new TestDb();
        var (ok, notFound, _) = await new ProgrammeAdminService(t.Db).UpdateAsync(999, Programme());
        Assert.False(ok);
        Assert.True(notFound);
    }

    [Fact]
    public async Task Hiding_a_programme_keeps_its_lessons_and_their_progress()
    {
        using var t = new TestDb();
        var svc = new ProgrammeAdminService(t.Db);
        var lesson = t.AddLesson();
        var user = t.AddUser();
        t.Db.Progress.Add(new Progress { UserId = user.Id, LessonId = lesson.Id, WatchTimeSeconds = 50, CompletionPercentage = 50 });
        await t.Db.SaveChangesAsync();
        var programmeId = lesson.ProgrammeId;

        await svc.UpdateAsync(programmeId, new ProgrammeUpsert("Programme", "", 1, false));

        Assert.Equal("INACTIVE", (await t.Db.Programmes.AsNoTracking().SingleAsync(p => p.Id == programmeId)).Status);
        Assert.Equal(1, await t.Db.Lessons.CountAsync());
        Assert.Equal(1, await t.Db.Progress.CountAsync());
    }

    [Fact]
    public async Task The_list_shows_lesson_counts_and_hidden_programmes_to_administrators()
    {
        using var t = new TestDb();
        var svc = new ProgrammeAdminService(t.Db);
        t.AddLesson(); t.AddLesson();                       // two programmes with one lesson each
        var (_, hidden, _) = await svc.CreateAsync(Programme("Hidden one", active: false));

        var list = await svc.ListAsync();

        Assert.Equal(3, list.Count);
        Assert.Equal(2, list.Count(p => p.LessonCount == 1));
        Assert.False(list.Single(p => p.Id == hidden).Active);
    }
}
