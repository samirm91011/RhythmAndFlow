using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

public class SouthAfricanDateTests
{
    [Fact]
    public void A_plan_ending_just_after_midnight_in_South_Africa_shows_the_South_African_day()
    {
        // 22:57 UTC on 4 Nov is 00:57 on 5 Nov in South Africa, which is what the app shows.
        Assert.Equal("5 Nov 2026", SubscriptionService.Day(new DateTime(2026, 11, 4, 22, 57, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void A_midday_time_keeps_its_day_and_a_missing_date_is_blank()
    {
        Assert.Equal("4 Nov 2026", SubscriptionService.Day(new DateTime(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("", SubscriptionService.Day(null));
    }
}
