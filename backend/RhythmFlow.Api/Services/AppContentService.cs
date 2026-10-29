using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;

namespace RhythmFlow.Api.Services;

public record MoodDto(string Label, string Emoji, string Color);
public record RhythmCardDto(string Title, string Subtitle);
public record JournalPromptsDto(string ReflectionTitle, string ReflectionHint, string GratitudeTitle, string GratitudeHint);
public record ExploreSectionDto(string Title, string Blurb, string Body);
public record ProductDto(string Name, string Price);
public record ShopDto(string Intro, List<ProductDto> Products);
public record HomeTextDto(string ClassTitle, string ClassBody, string BannerTitle, string BannerBody);
/// <summary>The wording the client can change from the admin screens: the check-in moods, affirmations, quotes, journal prompts, Home wording, the Explore sections with their articles, and the Shop.</summary>
public record AppContentDto(
    List<MoodDto> Moods, List<string> Affirmations, string HomeQuote, string RhythmQuote, List<RhythmCardDto> RhythmCards, JournalPromptsDto Journal,
    HomeTextDto? Home = null, List<ExploreSectionDto>? Explore = null, ShopDto? Shop = null);

/// <summary>
/// The text the app shows that the client wants to edit herself. Until she saves a change the built-in wording below is used, and
/// anything missing from what she saved falls back to it, so a screen is never empty. One row in the database holds her version.
/// </summary>
public class AppContentService(AppDbContext db)
{
    private const string RowKey = "app";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Regex Colour = new("^#[0-9A-Fa-f]{6}$");

    public static AppContentDto Defaults() => new(
        [
            new("Energised", "☀️", "#FFF1DC"), new("Tired", "🔋", "#E3ECFB"), new("Stressed", "☁️", "#F3E6EE"),
            new("Grounded", "🌿", "#DDEFEA"), new("Overwhelmed", "🌀", "#FDE9E1"), new("Happy", "😊", "#FFEFD9"),
            new("Low", "🌙", "#E5E8F7"), new("Need some space", "🤍", "#FBE4E3"),
        ],
        [
            "I am enough, exactly as I am, and I am always growing.",
            "My body is strong, and I move with kindness.",
            "I give myself permission to rest and to rise.",
            "Every small step brings me back to myself.",
            "I am free to express who I am through movement.",
            "I welcome joy into my body today.",
            "I am proud of how far I've come.",
        ],
        "A small check-in can lead to a big shift.",
        "Progress isn't about doing more, it's about coming back to yourself.",
        [new("Morning Reset", "10 mins"), new("Nourish Your Body", "Recipes"), new("Move & Release", "15 mins")],
        new("Today's Reflection", "What's on your mind?", "Gratitude", "What are you grateful for today?"),
        new("Join a class", "Move with Deni and the community. Browse upcoming classes and reserve your spot.", "Build a life that feels good.", "Unlock every programme with a monthly subscription."),
        [
            new("Feed Your Soul", "Mindfulness, meditation, emotional wellbeing.", ArticleText),
            new("Nourish Your Body", "Nutrition, food, self-care.", ArticleText),
            new("Open Your Mind", "Personal development, mindset, coaching.", ArticleText),
            new("Discover New Journeys", "Travel, experiences, trying something new.", ArticleText),
            new("Coaching & Programmes", "Guided support for your next chapter.", ArticleText),
            new("Shop", "Wellness products for everyday living.", ""),
        ],
        new("The Rhythm & Flow shop is coming soon to the app. For now, visit rhythmandflow.co.za to order.",
            [new("Yoga Mat", "R650"), new("Stainless Steel Bottle", "R450"), new("Resistance Bands", "R320"), new("Wellness Journal", "R250")]));

    private const string ArticleText =
        "Movement is not just about physical exercise; it is a way to freely express ourselves, connect with others, and find inner balance. Whether you are a beginner or an experienced mover, there is a place for you in this community.\n\n" +
        "Balance isn't about doing it all. It's about creating space for what matters most. Here are a few simple ways to bring more intention into your everyday life:\n\n" +
        "•  Start the day with a few slow breaths.\n•  Move in a way that feels kind to your body.\n•  End the day by noting one thing you're grateful for.";

    /// <summary>What the apps show: her saved version, with the built-in wording filling any gap.</summary>
    public async Task<AppContentDto> GetAsync()
    {
        var row = await db.AppContents.AsNoTracking().FirstOrDefaultAsync(x => x.Key == RowKey);
        var d = Defaults();
        if (row is null) return d;
        AppContentDto? saved;
        try { saved = JsonSerializer.Deserialize<AppContentDto>(row.Json, Json); } catch (JsonException) { return d; }
        if (saved is null) return d;
        return new AppContentDto(
            saved.Moods is { Count: > 0 } ? saved.Moods : d.Moods,
            saved.Affirmations is { Count: > 0 } ? saved.Affirmations : d.Affirmations,
            string.IsNullOrWhiteSpace(saved.HomeQuote) ? d.HomeQuote : saved.HomeQuote,
            string.IsNullOrWhiteSpace(saved.RhythmQuote) ? d.RhythmQuote : saved.RhythmQuote,
            saved.RhythmCards is { Count: 3 } ? saved.RhythmCards : d.RhythmCards,
            saved.Journal ?? d.Journal,
            saved.Home ?? d.Home,
            saved.Explore is { Count: 6 } ? saved.Explore : d.Explore,
            saved.Shop ?? d.Shop);
    }

    /// <summary>Checks what an administrator typed. Returns a message to show, or null if it is fine.</summary>
    public static string? Validate(AppContentDto c)
    {
        var d0 = Defaults();
        if (c.Moods is null || c.Moods.Count is < 1 or > 12) return "Keep between 1 and 12 moods.";
        foreach (var m in c.Moods)
        {
            if (string.IsNullOrWhiteSpace(m.Label) || m.Label.Trim().Length > 30) return "Each mood needs a name of up to 30 characters.";
            if (string.IsNullOrWhiteSpace(m.Emoji) || m.Emoji.Trim().Length > 16) return $"The mood \"{m.Label}\" needs one emoji.";
            if (!Colour.IsMatch(m.Color ?? "")) return $"The colour for \"{m.Label}\" must look like #FFF1DC.";
        }
        if (c.Moods.Select(m => m.Label.Trim().ToLowerInvariant()).Distinct().Count() != c.Moods.Count) return "Two moods have the same name.";
        if (c.Affirmations is null || c.Affirmations.Count is < 1 or > 60) return "Keep between 1 and 60 affirmations.";
        if (c.Affirmations.Any(a => string.IsNullOrWhiteSpace(a) || a.Trim().Length > 200)) return "Each affirmation can be up to 200 characters.";
        if (string.IsNullOrWhiteSpace(c.HomeQuote) || c.HomeQuote.Trim().Length > 200) return "The Home quote can be up to 200 characters.";
        if (string.IsNullOrWhiteSpace(c.RhythmQuote) || c.RhythmQuote.Trim().Length > 200) return "The Your Rhythm Today quote can be up to 200 characters.";
        if (c.RhythmCards is null || c.RhythmCards.Count != 3) return "There are three Your Rhythm Today cards.";
        if (c.RhythmCards.Any(r => string.IsNullOrWhiteSpace(r.Title) || r.Title.Trim().Length > 40 || (r.Subtitle ?? "").Trim().Length > 40)) return "Each card needs a title and a short line, up to 40 characters.";
        var j = c.Journal;
        if (j is null || new[] { j.ReflectionTitle, j.ReflectionHint, j.GratitudeTitle, j.GratitudeHint }.Any(s => string.IsNullOrWhiteSpace(s) || s.Trim().Length > 80)) return "The journal wording can be up to 80 characters each.";
        var h = c.Home ?? d0.Home!;
        if (new[] { h.ClassTitle, h.BannerTitle }.Any(s => string.IsNullOrWhiteSpace(s) || s.Trim().Length > 80) || new[] { h.ClassBody, h.BannerBody }.Any(s => string.IsNullOrWhiteSpace(s) || s.Trim().Length > 200)) return "The Home wording needs titles up to 80 characters and lines up to 200.";
        var ex = c.Explore ?? d0.Explore!;
        if (ex.Count != 6) return "There are six Explore sections.";
        if (ex.Any(e => string.IsNullOrWhiteSpace(e.Title) || e.Title.Trim().Length > 40 || string.IsNullOrWhiteSpace(e.Blurb) || e.Blurb.Trim().Length > 100 || (e.Body ?? "").Length > 6000)) return "Each Explore section needs a title (up to 40 characters), a short line (up to 100) and an article of up to 6000 characters.";
        if (ex.Select(e => e.Title.Trim().ToLowerInvariant()).Distinct().Count() != ex.Count) return "Two Explore sections have the same title.";
        var sh = c.Shop ?? d0.Shop!;
        if (string.IsNullOrWhiteSpace(sh.Intro) || sh.Intro.Trim().Length > 300) return "The Shop message can be up to 300 characters.";
        if (sh.Products is null || sh.Products.Count > 12) return "Keep up to 12 shop products.";
        if (sh.Products.Any(p => string.IsNullOrWhiteSpace(p.Name) || p.Name.Trim().Length > 40 || string.IsNullOrWhiteSpace(p.Price) || p.Price.Trim().Length > 20)) return "Each product needs a name (up to 40 characters) and a price such as R650.";
        return null;
    }

    public async Task<(bool Ok, string? Error)> SaveAsync(AppContentDto c)
    {
        var error = Validate(c);
        if (error is not null) return (false, error);
        var clean = new AppContentDto(
            c.Moods.Select(m => new MoodDto(m.Label.Trim(), m.Emoji.Trim(), m.Color.ToUpperInvariant())).ToList(),
            c.Affirmations.Select(a => a.Trim()).ToList(), c.HomeQuote.Trim(), c.RhythmQuote.Trim(),
            c.RhythmCards.Select(r => new RhythmCardDto(r.Title.Trim(), (r.Subtitle ?? "").Trim())).ToList(),
            new JournalPromptsDto(c.Journal.ReflectionTitle.Trim(), c.Journal.ReflectionHint.Trim(), c.Journal.GratitudeTitle.Trim(), c.Journal.GratitudeHint.Trim()),
            c.Home is null ? null : new HomeTextDto(c.Home.ClassTitle.Trim(), c.Home.ClassBody.Trim(), c.Home.BannerTitle.Trim(), c.Home.BannerBody.Trim()),
            c.Explore?.Select(e => new ExploreSectionDto(e.Title.Trim(), e.Blurb.Trim(), (e.Body ?? "").Trim())).ToList(),
            c.Shop is null ? null : new ShopDto(c.Shop.Intro.Trim(), c.Shop.Products.Select(p => new ProductDto(p.Name.Trim(), p.Price.Trim())).ToList()));
        var row = await db.AppContents.FirstOrDefaultAsync(x => x.Key == RowKey);
        if (row is null) db.AppContents.Add(row = new AppContent { Key = RowKey });
        row.Json = JsonSerializer.Serialize(clean, Json); row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>Goes back to the built-in wording.</summary>
    public async Task ResetAsync()
    {
        var row = await db.AppContents.FirstOrDefaultAsync(x => x.Key == RowKey);
        if (row is null) return;
        db.AppContents.Remove(row);
        await db.SaveChangesAsync();
    }
}
