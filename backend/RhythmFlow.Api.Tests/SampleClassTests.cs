using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;

namespace RhythmFlow.Api.Tests;

public class SampleClassTests
{
    private static ClassSession Placeholder(DateTime start, string status = "SCHEDULED") => new()
    {
        Name = "Sample", Location = "Rhythm & Flow Studio (placeholder)", CoachName = "Deni",
        StartTime = start, EndTime = start.AddHours(1), Capacity = 20, Status = status,
    };

    private static Task<int> UpcomingAsync(TestDb t) =>
        t.Db.Classes.CountAsync(c => c.Status == "SCHEDULED" && c.StartTime > DateTime.UtcNow);

    [Fact]
    public async Task An_empty_timetable_is_filled_with_upcoming_placeholder_classes()
    {
        using var t = new TestDb();

        await SeedData.EnsureUpcomingClassesAsync(t.Db);

        Assert.True(await UpcomingAsync(t) >= 5);
        Assert.All(await t.Db.Classes.ToListAsync(), c => Assert.Contains("(placeholder)", c.Location));
    }

    [Fact]
    public async Task A_timetable_that_has_run_dry_is_topped_up_in_the_future()
    {
        using var t = new TestDb();
        t.Db.Classes.AddRange(Placeholder(DateTime.UtcNow.AddDays(-5)), Placeholder(DateTime.UtcNow.AddDays(-3)), Placeholder(DateTime.UtcNow.AddDays(1)));
        await t.Db.SaveChangesAsync();

        await SeedData.EnsureUpcomingClassesAsync(t.Db);

        Assert.True(await UpcomingAsync(t) >= 5);
        Assert.Equal(2, await t.Db.Classes.CountAsync(c => c.StartTime < DateTime.UtcNow));   // the past classes were left alone
    }

    [Fact]
    public async Task Nothing_is_added_when_enough_classes_are_already_coming_up()
    {
        using var t = new TestDb();
        for (var i = 1; i <= 6; i++) t.Db.Classes.Add(Placeholder(DateTime.UtcNow.AddDays(i)));
        await t.Db.SaveChangesAsync();

        await SeedData.EnsureUpcomingClassesAsync(t.Db);

        Assert.Equal(6, await t.Db.Classes.CountAsync());
    }

    [Fact]
    public async Task Cancelled_classes_do_not_count_as_upcoming()
    {
        using var t = new TestDb();
        for (var i = 1; i <= 6; i++) t.Db.Classes.Add(Placeholder(DateTime.UtcNow.AddDays(i), "CANCELLED"));
        await t.Db.SaveChangesAsync();

        await SeedData.EnsureUpcomingClassesAsync(t.Db);

        Assert.True(await UpcomingAsync(t) >= 5);
    }

    [Fact]
    public async Task Once_the_studio_has_added_a_class_of_its_own_nothing_more_is_ever_added()
    {
        using var t = new TestDb();
        t.AddClass(startsIn: TimeSpan.FromDays(-10));   // a real class (location "Studio"), now in the past

        await SeedData.EnsureUpcomingClassesAsync(t.Db);

        Assert.Equal(1, await t.Db.Classes.CountAsync());
    }
}
