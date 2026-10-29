using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;

namespace RhythmFlow.Api.Services;

public record SuggestionDto(string Mood, string Category, int? LessonId, string? Title, int DurationSeconds, string Reason);
public record FeelJourneyDto(string From, string To, int Count);
public record FeelInsightDto(int CheckIns, int PracticesLogged, string? TopFeeling, List<FeelJourneyDto> Journeys, string? Message);

/// <summary>
/// The "feel-better loop": a mood check-in suggests a practice that suits it, and the "How do you feel now?" answers after practising
/// are turned into a short, kind summary of how practising has been leaving the person feeling.
/// </summary>
public static class FeelService
{
    /// <summary>The practice type that suits each of the built-in moods. Moods the client adds herself fall back to Yoga.</summary>
    private static readonly Dictionary<string, (string Category, string Reason)> ForMood = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Energised"] = ("Dance", "You have energy to share. Let it move."),
        ["Tired"] = ("Stretch & Mobility", "Something gentle to wake the body without asking too much."),
        ["Stressed"] = ("Breathwork", "Slow breathing is one of the quickest ways to settle."),
        ["Grounded"] = ("Yoga", "Keep that steadiness with a flow."),
        ["Overwhelmed"] = ("Meditation", "A few quiet minutes to clear some space."),
        ["Happy"] = ("Dance", "Carry that happiness into movement."),
        ["Low"] = ("Yoga", "Gentle movement can lift the edges of a low day."),
        ["Need some space"] = ("Meditation", "Space to be with yourself, nothing to achieve."),
    };

    public static (string Category, string Reason) Pick(string? mood) =>
        mood is not null && ForMood.TryGetValue(mood.Trim(), out var m) ? m : ("Yoga", "A gentle flow to move, breathe and reconnect.");

    /// <summary>One practice of the suggested type that this person can watch, preferring one they have not finished. Null if there is none.</summary>
    public static async Task<SuggestionDto> SuggestAsync(AppDbContext db, Guid userId, int tier, bool isAdmin, string? mood)
    {
        var (category, reason) = Pick(mood);
        var lessons = await db.Lessons.Include(l => l.Programme).Where(l => l.Programme!.Status == "ACTIVE" && l.Category == category)
            .OrderBy(l => l.ProgrammeId).ThenBy(l => l.SequenceNumber).ToListAsync();
        var open = lessons.Where(l => EntitlementService.CanWatch(l, tier, isAdmin)).ToList();
        var done = await db.Progress.Where(p => p.UserId == userId && p.CompletionPercentage >= 95).Select(p => p.LessonId).ToListAsync();
        var pick = open.FirstOrDefault(l => !done.Contains(l.Id)) ?? open.FirstOrDefault();
        return pick is null
            ? new SuggestionDto(mood ?? "", category, null, null, 0, reason)
            : new SuggestionDto(mood ?? "", category, pick.Id, pick.Title, pick.DurationSeconds, reason);
    }

    private static readonly string[] Positive = ["Calmer", "More energised", "Lighter", "Stronger", "More present"];

    /// <summary>
    /// Looks at the last 30 days. A check-in followed within six hours by a "how do you feel now" answer counts as one journey, for example
    /// Stressed to Calmer. Returns the most common journeys and a short message, or no message when there is nothing to say yet.
    /// </summary>
    public static async Task<FeelInsightDto> InsightAsync(AppDbContext db, Guid userId, DateTime now)
    {
        var since = now.AddDays(-30);
        var rows = await db.Journal.AsNoTracking().Where(j => j.UserId == userId && j.CreatedAt >= since && j.Mood != "")
            .OrderBy(j => j.CreatedAt).Select(j => new { j.Kind, j.Mood, j.CreatedAt }).ToListAsync();
        var checkIns = rows.Where(r => r.Kind == "CHECKIN").ToList();
        var after = rows.Where(r => r.Kind == "REFLECTION" && Positive.Contains(r.Mood)).ToList();

        var journeys = new List<(string From, string To)>();
        foreach (var a in after)
        {
            var before = checkIns.LastOrDefault(c => c.CreatedAt <= a.CreatedAt && a.CreatedAt - c.CreatedAt <= TimeSpan.FromHours(6));
            if (before is not null) journeys.Add((before.Mood, a.Mood));
        }
        var top = journeys.GroupBy(j => j).Select(g => new FeelJourneyDto(g.Key.From, g.Key.To, g.Count())).OrderByDescending(j => j.Count).ThenBy(j => j.From).Take(3).ToList();
        var topFeeling = after.GroupBy(a => a.Mood).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Select(g => g.Key).FirstOrDefault();

        string? message = null;
        if (top.Count > 0)
        {
            var j = top[0];
            message = j.Count > 1
                ? $"When you feel {j.From.ToLowerInvariant()}, practising has left you feeling {j.To.ToLowerInvariant()}. That has happened {j.Count} times this month."
                : $"You checked in feeling {j.From.ToLowerInvariant()} and finished your practice feeling {j.To.ToLowerInvariant()}.";
        }
        else if (after.Count > 0)
        {
            message = after.Count > 1
                ? $"You have noted feeling {topFeeling!.ToLowerInvariant()} after practising. {after.Count} practices this month have left you feeling good."
                : $"You noted feeling {topFeeling!.ToLowerInvariant()} after practising.";
        }
        return new FeelInsightDto(checkIns.Count, after.Count, topFeeling, top, message);
    }
}
