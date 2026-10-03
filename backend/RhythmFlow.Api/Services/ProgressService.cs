using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;

namespace RhythmFlow.Api.Services;

public class ProgressService(AppDbContext db)
{
    /// <summary>Completion = watch time / lesson duration x 100, capped at 100 (BR-10). Watch time only moves forward.</summary>
    public async Task<ProgressDto> UpdateAsync(Guid userId, Lesson lesson, int watchSeconds)
    {
        var p = await db.Progress.FirstOrDefaultAsync(x => x.UserId == userId && x.LessonId == lesson.Id);
        if (p is null)
        {
            p = new Progress { UserId = userId, LessonId = lesson.Id };
            db.Progress.Add(p);
        }
        var watch = Math.Clamp(Math.Max(p.WatchTimeSeconds, watchSeconds), 0, lesson.DurationSeconds);
        p.WatchTimeSeconds = watch;
        p.CompletionPercentage = lesson.DurationSeconds == 0 ? 0 :
            Math.Min(100m, Math.Round(watch * 100m / lesson.DurationSeconds, 2));
        p.Completed = p.CompletionPercentage >= 95m;
        p.LastWatchedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return new ProgressDto(lesson.Id, p.WatchTimeSeconds, p.CompletionPercentage, p.Completed);
    }

    public async Task<ProgressSummaryDto> SummaryAsync(Guid userId)
    {
        var rows = await db.Progress.Where(p => p.UserId == userId).ToListAsync();
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return new ProgressSummaryDto(
            rows.Count(r => r.Completed),
            rows.Count(r => !r.Completed && r.WatchTimeSeconds > 0),
            rows.Count(r => r.LastWatchedAt >= monthStart),
            rows.Sum(r => r.WatchTimeSeconds) / 60);
    }
}
