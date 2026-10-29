using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

/// <summary>The wording the client edits from the admin screens: moods, affirmations, quotes and journal prompts.</summary>
public class AppContentTests
{
    private static AppContentDto Edited() => AppContentService.Defaults() with
    {
        Moods = [new("Calm", "😌", "#e3f0ed"), new("Restless", "🌪️", "#FDE9E1")],
        Affirmations = ["I am here.", "I am enough."],
        HomeQuote = "Begin where you are.",
    };

    [Fact]
    public async Task Until_she_saves_a_change_the_apps_get_the_built_in_wording()
    {
        using var t = new TestDb();
        var c = await new AppContentService(t.Db).GetAsync();

        Assert.Equal(8, c.Moods.Count); Assert.Equal("Energised", c.Moods[0].Label);
        Assert.Equal(7, c.Affirmations.Count);
        Assert.Equal(3, c.RhythmCards.Count);
        Assert.Equal("Today's Reflection", c.Journal.ReflectionTitle);
    }

    [Fact]
    public async Task A_saved_change_is_what_every_screen_then_reads_and_colours_are_tidied()
    {
        using var t = new TestDb(); var svc = new AppContentService(t.Db);

        var (ok, error) = await svc.SaveAsync(Edited());
        var c = await new AppContentService(t.Db).GetAsync();

        Assert.True(ok, error);
        Assert.Equal(["Calm", "Restless"], c.Moods.Select(m => m.Label));
        Assert.Equal("#E3F0ED", c.Moods[0].Color);
        Assert.Equal(["I am here.", "I am enough."], c.Affirmations);
        Assert.Equal("Begin where you are.", c.HomeQuote);
        Assert.Equal("Progress isn't about doing more, it's about coming back to yourself.", c.RhythmQuote);   // untouched
    }

    [Fact]
    public async Task Saving_again_replaces_the_one_saved_version_and_reset_goes_back_to_the_built_in_wording()
    {
        using var t = new TestDb(); var svc = new AppContentService(t.Db);
        await svc.SaveAsync(Edited());
        await svc.SaveAsync(Edited() with { HomeQuote = "Second version." });
        Assert.Equal(1, t.Db.AppContents.Count());
        Assert.Equal("Second version.", (await svc.GetAsync()).HomeQuote);

        await svc.ResetAsync();

        Assert.Empty(t.Db.AppContents);
        Assert.Equal("A small check-in can lead to a big shift.", (await svc.GetAsync()).HomeQuote);
    }

    [Fact]
    public async Task A_broken_saved_row_falls_back_instead_of_breaking_the_apps()
    {
        using var t = new TestDb();
        t.Db.AppContents.Add(new RhythmFlow.Api.Domain.AppContent { Key = "app", Json = "{ not json" }); t.Db.SaveChanges();
        Assert.Equal(8, (await new AppContentService(t.Db).GetAsync()).Moods.Count);

        t.Db.AppContents.RemoveRange(t.Db.AppContents); t.Db.AppContents.Add(new RhythmFlow.Api.Domain.AppContent { Key = "app", Json = "{\"homeQuote\":\"Only this\"}" }); t.Db.SaveChanges();
        var c = await new AppContentService(t.Db).GetAsync();
        Assert.Equal("Only this", c.HomeQuote); Assert.Equal(8, c.Moods.Count); Assert.Equal(7, c.Affirmations.Count);   // the rest is the built-in wording
    }

    [Fact]
    public void Mistakes_are_refused_with_a_message_she_can_act_on()
    {
        var d = AppContentService.Defaults();
        Assert.Contains("between 1 and 12 moods", AppContentService.Validate(d with { Moods = [] }));
        Assert.Contains("between 1 and 12 moods", AppContentService.Validate(d with { Moods = Enumerable.Range(0, 13).Select(i => new MoodDto("M" + i, "😊", "#FFFFFF")).ToList() }));
        Assert.Contains("colour", AppContentService.Validate(d with { Moods = [new("Calm", "😌", "blue")] }));
        Assert.Contains("one emoji", AppContentService.Validate(d with { Moods = [new("Calm", "", "#FFFFFF")] }));
        Assert.Contains("same name", AppContentService.Validate(d with { Moods = [new("Calm", "😌", "#FFFFFF"), new("calm ", "😊", "#FFFFFF")] }));
        Assert.Contains("affirmations", AppContentService.Validate(d with { Affirmations = [] }));
        Assert.Contains("200 characters", AppContentService.Validate(d with { Affirmations = [new string('x', 201)] }));
        Assert.Contains("Home quote", AppContentService.Validate(d with { HomeQuote = "  " }));
        Assert.Contains("three", AppContentService.Validate(d with { RhythmCards = d.RhythmCards.Take(2).ToList() }));
        Assert.Contains("journal", AppContentService.Validate(d with { Journal = d.Journal with { GratitudeHint = "" } }));
        Assert.Null(AppContentService.Validate(d));
    }

    [Fact]
    public async Task Home_wording_explore_articles_and_shop_are_editable_and_default_when_missing()
    {
        using var t = new TestDb(); var svc = new AppContentService(t.Db);
        var d = AppContentService.Defaults();
        Assert.Equal(6, d.Explore!.Count); Assert.Equal(4, d.Shop!.Products.Count);

        var ex = d.Explore.ToList(); ex[0] = new("Feed Your Spirit", "New line.", "My own article.\n\nSecond paragraph.");
        var (ok, error) = await svc.SaveAsync(d with { Explore = ex, Shop = new("Open on Saturdays.", [new("Mat", "R700")]), Home = d.Home! with { BannerTitle = "Welcome." } });
        var c = await svc.GetAsync();

        Assert.True(ok, error);
        Assert.Equal("Feed Your Spirit", c.Explore![0].Title); Assert.Equal("My own article.\n\nSecond paragraph.", c.Explore[0].Body);
        Assert.Equal("Nourish Your Body", c.Explore[1].Title);
        Assert.Equal(["Mat"], c.Shop!.Products.Select(p => p.Name)); Assert.Equal("Welcome.", c.Home!.BannerTitle);

        t.Db.AppContents.RemoveRange(t.Db.AppContents); t.Db.AppContents.Add(new RhythmFlow.Api.Domain.AppContent { Key = "app", Json = "{\"homeQuote\":\"Only this\"}" }); t.Db.SaveChanges();
        var old = await svc.GetAsync();   // content saved before these fields existed
        Assert.Equal(6, old.Explore!.Count); Assert.Equal("Join a class", old.Home!.ClassTitle);
    }

    [Fact]
    public void Explore_home_and_shop_mistakes_are_refused()
    {
        var d = AppContentService.Defaults();
        Assert.Contains("six Explore", AppContentService.Validate(d with { Explore = d.Explore!.Take(5).ToList() }));
        var dup = d.Explore!.ToList(); dup[1] = dup[1] with { Title = "feed your soul" };
        Assert.Contains("same title", AppContentService.Validate(d with { Explore = dup }));
        var long1 = d.Explore!.ToList(); long1[0] = long1[0] with { Body = new string('x', 6001) };
        Assert.Contains("6000", AppContentService.Validate(d with { Explore = long1 }));
        Assert.Contains("Shop message", AppContentService.Validate(d with { Shop = d.Shop! with { Intro = "" } }));
        Assert.Contains("price", AppContentService.Validate(d with { Shop = d.Shop! with { Products = [new("Mat", "")] } }));
        Assert.Contains("Home wording", AppContentService.Validate(d with { Home = d.Home! with { ClassTitle = "" } }));
        Assert.Null(AppContentService.Validate(d with { Shop = d.Shop! with { Products = [] } }));
    }

    [Fact]
    public async Task A_refused_change_saves_nothing()
    {
        using var t = new TestDb();
        var (ok, _) = await new AppContentService(t.Db).SaveAsync(AppContentService.Defaults() with { Moods = [] });
        Assert.False(ok); Assert.Empty(t.Db.AppContents);
    }
}
