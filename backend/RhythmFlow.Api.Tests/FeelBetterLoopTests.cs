using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

/// <summary>The feel-better loop: a mood suggests a practice, and "how do you feel now" answers become a kind summary.</summary>
public class FeelBetterLoopTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    private static void Entry(TestDb t, Guid user, string kind, string mood, DateTime at)
    {
        t.Db.Journal.Add(new JournalEntry { UserId = user, Kind = kind, Mood = mood, CreatedAt = at });
        t.Db.SaveChanges();
    }

    private static (FitnessProgramme Free, FitnessProgramme Paid) Programmes(TestDb t)
    {
        var free = new FitnessProgramme { Name = "Free", Description = "d", MinTier = 1, Status = "ACTIVE" };
        var paid = new FitnessProgramme { Name = "Paid", Description = "d", MinTier = 2, Status = "ACTIVE" };
        t.Db.Programmes.AddRange(free, paid); t.Db.SaveChanges();
        return (free, paid);
    }

    private static Lesson Lesson(TestDb t, FitnessProgramme p, string title, string category, bool preview, int seq = 1)
    {
        var l = new Lesson { ProgrammeId = p.Id, Title = title, Description = "d", Category = category, IsPreview = preview, SequenceNumber = seq, DurationSeconds = 600 };
        t.Db.Lessons.Add(l); t.Db.SaveChanges();
        return l;
    }

    [Theory]
    [InlineData("Stressed", "Breathwork")]
    [InlineData("stressed ", "Breathwork")]
    [InlineData("Tired", "Stretch & Mobility")]
    [InlineData("Overwhelmed", "Meditation")]
    [InlineData("Energised", "Dance")]
    [InlineData("Some mood the client added", "Yoga")]
    [InlineData(null, "Yoga")]
    public void Each_mood_suggests_a_kind_of_practice_and_unknown_moods_fall_back_to_yoga(string? mood, string category) =>
        Assert.Equal(category, FeelService.Pick(mood).Category);

    [Fact]
    public async Task The_suggestion_is_a_practice_of_that_type_the_person_can_actually_watch()
    {
        using var t = new TestDb(); var u = t.AddUser(); var (free, paid) = Programmes(t);
        var locked = Lesson(t, paid, "Members breath", "Breathwork", preview: false);
        var open = Lesson(t, free, "Free breath", "Breathwork", preview: true);

        var s = await FeelService.SuggestAsync(t.Db, u.Id, tier: 0, isAdmin: false, "Stressed");

        Assert.Equal(open.Id, s.LessonId); Assert.Equal("Breathwork", s.Category); Assert.Equal("Free breath", s.Title);
        Assert.NotEqual(locked.Id, s.LessonId);
    }

    [Fact]
    public async Task A_practice_not_yet_finished_is_preferred_and_a_subscriber_can_be_offered_members_practices()
    {
        using var t = new TestDb(); var u = t.AddUser(); var (free, paid) = Programmes(t);
        var first = Lesson(t, free, "First", "Yoga", preview: true, seq: 1);
        var second = Lesson(t, free, "Second", "Yoga", preview: true, seq: 2);
        t.Db.Progress.Add(new Progress { UserId = u.Id, LessonId = first.Id, CompletionPercentage = 100 }); t.Db.SaveChanges();

        Assert.Equal(second.Id, (await FeelService.SuggestAsync(t.Db, u.Id, 0, false, "Grounded")).LessonId);

        var members = Lesson(t, paid, "Members flow", "Meditation", preview: false);
        Assert.Null((await FeelService.SuggestAsync(t.Db, u.Id, 0, false, "Overwhelmed")).LessonId);
        Assert.Equal(members.Id, (await FeelService.SuggestAsync(t.Db, u.Id, 2, false, "Overwhelmed")).LessonId);
    }

    [Fact]
    public async Task With_no_practice_of_that_type_the_suggestion_still_answers_with_the_type_and_no_lesson()
    {
        using var t = new TestDb(); var u = t.AddUser();
        var s = await FeelService.SuggestAsync(t.Db, u.Id, 0, false, "Tired");
        Assert.Null(s.LessonId); Assert.Equal("Stretch & Mobility", s.Category); Assert.False(string.IsNullOrEmpty(s.Reason));
    }

    [Fact]
    public async Task Nothing_is_said_until_there_is_something_true_to_say()
    {
        using var t = new TestDb(); var u = t.AddUser();
        Entry(t, u.Id, "CHECKIN", "Stressed", Now.AddHours(-2));

        var i = await FeelService.InsightAsync(t.Db, u.Id, Now);

        Assert.Null(i.Message); Assert.Equal(1, i.CheckIns); Assert.Equal(0, i.PracticesLogged);
    }

    [Fact]
    public async Task A_check_in_followed_by_a_better_feeling_is_a_journey_and_repeats_are_counted()
    {
        using var t = new TestDb(); var u = t.AddUser();
        Entry(t, u.Id, "CHECKIN", "Stressed", Now.AddDays(-3).AddHours(-1)); Entry(t, u.Id, "REFLECTION", "Calmer", Now.AddDays(-3));
        Entry(t, u.Id, "CHECKIN", "Stressed", Now.AddDays(-1).AddHours(-1)); Entry(t, u.Id, "REFLECTION", "Calmer", Now.AddDays(-1));
        Entry(t, u.Id, "CHECKIN", "Tired", Now.AddHours(-3)); Entry(t, u.Id, "REFLECTION", "Lighter", Now.AddHours(-2));

        var i = await FeelService.InsightAsync(t.Db, u.Id, Now);

        Assert.Equal(("Stressed", "Calmer", 2), (i.Journeys[0].From, i.Journeys[0].To, i.Journeys[0].Count));
        Assert.Equal(2, i.Journeys.Count); Assert.Equal(3, i.PracticesLogged);
        Assert.Equal("When you feel stressed, practising has left you feeling calmer. That has happened 2 times this month.", i.Message);
    }

    [Fact]
    public async Task A_feeling_with_no_recent_check_in_still_counts_as_a_kind_message_and_old_or_other_answers_are_ignored()
    {
        using var t = new TestDb(); var u = t.AddUser(); var other = t.AddUser("Other");
        Entry(t, u.Id, "REFLECTION", "Stronger", Now.AddDays(-2));
        Entry(t, u.Id, "REFLECTION", "Other", Now.AddDays(-2));                       // "Other" says nothing about how they feel
        Entry(t, u.Id, "REFLECTION", "Calmer", Now.AddDays(-45));                     // outside the last 30 days
        Entry(t, other.Id, "REFLECTION", "Calmer", Now.AddDays(-1));                  // someone else's

        var i = await FeelService.InsightAsync(t.Db, u.Id, Now);

        Assert.Equal(1, i.PracticesLogged); Assert.Empty(i.Journeys); Assert.Equal("Stronger", i.TopFeeling);
        Assert.Equal("You noted feeling stronger after practising.", i.Message);
    }

    [Fact]
    public async Task A_check_in_more_than_six_hours_before_is_not_paired_with_the_practice()
    {
        using var t = new TestDb(); var u = t.AddUser();
        Entry(t, u.Id, "CHECKIN", "Low", Now.AddHours(-10)); Entry(t, u.Id, "REFLECTION", "Lighter", Now.AddHours(-1));

        var i = await FeelService.InsightAsync(t.Db, u.Id, Now);

        Assert.Empty(i.Journeys); Assert.Equal("You noted feeling lighter after practising.", i.Message);
    }
}
