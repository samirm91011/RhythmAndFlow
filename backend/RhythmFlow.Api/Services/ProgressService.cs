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
        var lessonId = lesson.Id; var duration = lesson.DurationSeconds;
        for (var attempt = 0; ; attempt++)
        {
            var p = await db.Progress.FirstOrDefaultAsync(x => x.UserId == userId && x.LessonId == lessonId);
            var isNew = p is null;
            if (p is null)
            {
                p = new Progress { UserId = userId, LessonId = lessonId };
                db.Progress.Add(p);
            }
            var watch = Math.Clamp(Math.Max(p.WatchTimeSeconds, watchSeconds), 0, duration);
            p.WatchTimeSeconds = watch;
            p.CompletionPercentage = duration == 0 ? 0 : Math.Min(100m, Math.Round(watch * 100m / duration, 2));
            p.Completed = p.CompletionPercentage >= 95m;
            p.LastWatchedAt = DateTime.UtcNow;
            try
            {
                await db.SaveChangesAsync();
                return new ProgressDto(lessonId, p.WatchTimeSeconds, p.CompletionPercentage, p.Completed);
            }
            catch (DbUpdateException) when (isNew && attempt == 0)
            {
                // The very first save for a lesson can arrive twice at once (the player reports when it pauses and again when the person
                // leaves). The other request won and there is now a row, so forget ours and update that one instead of failing.
                db.ChangeTracker.Clear();
            }
        }
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
