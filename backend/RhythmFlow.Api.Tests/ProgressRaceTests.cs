using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

/// <summary>Two first-time progress reports for the same lesson arriving together used to make one of them fail with a database error.</summary>
public class ProgressRaceTests
{
    /// <summary>On the first save, quietly saves a competing row first, exactly as the other request would have.</summary>
    private sealed class CompetingInsert(Func<AppDbContext> other, Guid userId, int lessonId) : SaveChangesInterceptor
    {
        private bool _done;
        public override InterceptionResult<int> SavingChanges(DbContextEventData e, InterceptionResult<int> result)
        {
            if (_done) return result;
            _done = true;
            using var rival = other();
            rival.Progress.Add(new Progress { UserId = userId, LessonId = lessonId, WatchTimeSeconds = 5, CompletionPercentage = 5, LastWatchedAt = DateTime.UtcNow });
            rival.SaveChanges();
            return result;
        }
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> result, CancellationToken ct = default) =>
            SavingChanges(e, result);
    }

    [Fact]
    public async Task A_first_report_that_loses_the_race_updates_the_row_the_other_one_made_instead_of_failing()
    {
        using var conn = new SqliteConnection("Data Source=:memory:"); conn.Open();
        AppDbContext Make(params IInterceptor[] i) => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).AddInterceptors(i).Options);
        using var setup = Make(); setup.Database.EnsureCreated();
        var user = new User { FullName = "A", Username = "a1", Email = "a@example.com" };
        var programme = new FitnessProgramme { Name = "P", Lessons = { new Lesson { Title = "L", DurationSeconds = 100, VideoReference = "x" } } };
        setup.Users.Add(user); setup.Programmes.Add(programme); setup.SaveChanges();
        var lesson = programme.Lessons.Single();

        using var db = Make(new CompetingInsert(() => Make(), user.Id, lesson.Id));
        var result = await new ProgressService(db).UpdateAsync(user.Id, lesson, 40);

        Assert.Equal(40, result.WatchTimeSeconds);                 // the later, bigger number wins
        using var check = Make();
        var row = Assert.Single(check.Progress.AsNoTracking().Where(p => p.UserId == user.Id));
        Assert.Equal(40, row.WatchTimeSeconds);
    }
}
