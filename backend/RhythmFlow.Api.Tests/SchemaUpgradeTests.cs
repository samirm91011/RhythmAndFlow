using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;

namespace RhythmFlow.Api.Tests;

public class SchemaUpgradeTests
{
    [Fact]
    public async Task A_database_created_before_push_existed_gets_the_missing_table()
    {
        using var t = new TestDb();
        await t.Db.Database.ExecuteSqlRawAsync("DROP TABLE \"DeviceTokens\"");     // what an earlier version's database looks like

        await SchemaUpgrades.ApplyAsync(t.Db);

        t.Db.DeviceTokens.Add(new DeviceToken { UserId = Guid.NewGuid(), Token = "token-aaaaaaaaaaaaaaaaaaaaaaaaaaaa" });
        await t.Db.SaveChangesAsync();
        Assert.Equal(1, await t.Db.DeviceTokens.CountAsync());
    }

    [Fact]
    public async Task Running_it_again_on_an_up_to_date_database_changes_nothing()
    {
        using var t = new TestDb();
        t.Db.DeviceTokens.Add(new DeviceToken { UserId = Guid.NewGuid(), Token = "token-bbbbbbbbbbbbbbbbbbbbbbbbbbbb" });
        await t.Db.SaveChangesAsync();

        await SchemaUpgrades.ApplyAsync(t.Db);
        await SchemaUpgrades.ApplyAsync(t.Db);

        Assert.Equal(1, await t.Db.DeviceTokens.CountAsync());
    }

    [Fact]
    public async Task The_recreated_table_keeps_the_unique_token_rule()
    {
        using var t = new TestDb();
        await t.Db.Database.ExecuteSqlRawAsync("DROP TABLE \"DeviceTokens\"");
        await SchemaUpgrades.ApplyAsync(t.Db);
        t.Db.DeviceTokens.Add(new DeviceToken { UserId = Guid.NewGuid(), Token = "token-cccccccccccccccccccccccccccc" });
        await t.Db.SaveChangesAsync();
        t.Db.DeviceTokens.Add(new DeviceToken { UserId = Guid.NewGuid(), Token = "token-cccccccccccccccccccccccccccc" });

        await Assert.ThrowsAsync<DbUpdateException>(() => t.Db.SaveChangesAsync());
    }

    private static Lesson Lesson(int programmeId, string category) =>
        new() { ProgrammeId = programmeId, Title = category + " lesson", Category = category, DurationSeconds = 60, VideoReference = "https://x/y.mp4" };

    [Fact]
    public async Task Lessons_saved_under_the_old_category_names_are_renamed()
    {
        using var t = new TestDb();
        var prog = new FitnessProgramme { Name = "P", MinTier = 1 };
        t.Db.Programmes.Add(prog); await t.Db.SaveChangesAsync();
        t.Db.Lessons.AddRange(Lesson(prog.Id, "Barre"), Lesson(prog.Id, "Stretch"), Lesson(prog.Id, "Yoga"), Lesson(prog.Id, "Breathwork"));
        await t.Db.SaveChangesAsync();

        await SchemaUpgrades.ApplyAsync(t.Db);

        var cats = (await t.Db.Lessons.AsNoTracking().Select(l => l.Category).ToListAsync()).OrderBy(c => c).ToArray();
        Assert.Equal(["Barre & Pilates", "Breathwork", "Stretch & Mobility", "Yoga"], cats);
    }

    [Fact]
    public async Task The_category_rename_can_run_again_and_leaves_new_names_alone()
    {
        using var t = new TestDb();
        var prog = new FitnessProgramme { Name = "P", MinTier = 1 };
        t.Db.Programmes.Add(prog); await t.Db.SaveChangesAsync();
        t.Db.Lessons.Add(Lesson(prog.Id, "Barre & Pilates")); await t.Db.SaveChangesAsync();

        await SchemaUpgrades.ApplyAsync(t.Db);
        await SchemaUpgrades.ApplyAsync(t.Db);

        Assert.Equal("Barre & Pilates", (await t.Db.Lessons.AsNoTracking().SingleAsync()).Category);
    }
}