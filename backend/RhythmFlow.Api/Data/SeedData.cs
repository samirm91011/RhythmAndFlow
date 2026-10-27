using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Domain;

namespace RhythmFlow.Api.Data;

/// <summary>The two memberships exactly as the client wrote them (feedback of 20 Oct 2026). One copy, used by the seed and by the upgrade of older databases.</summary>
public static class ClientPlans
{
    public record Copy(string Name, decimal Price, string Description, string Features);

    public static readonly Copy Library = new("Move & Breathe", 149,
        "Your space to move, pause and reconnect with yourself, at your own pace.",
        "Full on-demand video library|Yoga, barre, dance, mobility and stretching|Meditation, breathwork and strength classes|Beginner, intermediate and advanced options");

    public static readonly Copy Guided = new("Move, Breathe & Grow", 249,
        "Build a more intentional practice through movement, mindful breathing and guided programmes that nurture your wellbeing.",
        "Everything in Move & Breathe|Access to all structured programmes|Guided journeys combining movement, mobility, relaxation and wellbeing|A more structured way to establish consistency and explore new practices");

    /// <summary>The client said annual should work out at roughly one or two months free. Twelve months for ten is a starting value she can change in Admin tools.</summary>
    public const decimal AnnualMonthsCharged = 10;
}

/// <summary>Demo data. Plans, prices and content are placeholders until the client confirms them.</summary>
public static class SeedData
{
    // Public test clips (hosted by Google/ExoPlayer test media and test-videos.co.uk) stand in for the client's videos.
    private static readonly (string Url, int Seconds) ClipA = ("https://storage.googleapis.com/exoplayer-test-media-1/mp4/android-screens-10s.mp4", 10);
    private static readonly (string Url, int Seconds) ClipB = ("https://test-videos.co.uk/vids/bigbuckbunny/mp4/h264/360/Big_Buck_Bunny_360_10s_1MB.mp4", 10);
    private static readonly (string Url, int Seconds) ClipC = ("https://storage.googleapis.com/exoplayer-test-media-0/BigBuckBunny_320x180.mp4", 596);

    public static async Task RunAsync(AppDbContext db, IConfiguration cfg)
    {
        await SeedUsersAsync(db, cfg);

        if (!await db.Plans.AnyAsync())
        {
            // The client's two memberships. Prices are launch prices and can be changed in Admin tools.
            db.Plans.AddRange(
                new SubscriptionPlan
                {
                    Name = ClientPlans.Library.Name, Price = ClientPlans.Library.Price, Tier = 1,
                    Description = ClientPlans.Library.Description, Features = ClientPlans.Library.Features,
                    AnnualPrice = ClientPlans.Library.Price * ClientPlans.AnnualMonthsCharged,
                },
                new SubscriptionPlan
                {
                    Name = ClientPlans.Guided.Name, Price = ClientPlans.Guided.Price, Tier = 2,
                    Description = ClientPlans.Guided.Description, Features = ClientPlans.Guided.Features,
                    AnnualPrice = ClientPlans.Guided.Price * ClientPlans.AnnualMonthsCharged,
                });
        }
        if (!await db.Programmes.AnyAsync())
        {
            var move = new FitnessProgramme { Name = "Move & Release", Description = "Gentle practices to release tension and feel lighter.", MinTier = 1 };
            var dance = new FitnessProgramme { Name = "Dance with Joy", Description = "Feel-good dance movement for every level.", MinTier = 1 };
            var mobility = new FitnessProgramme { Name = "Mindful Mobility", Description = "Stretch, restore and move well for life.", MinTier = 1 };
            // A structured programme: only the second membership opens it.
            var masters = new FitnessProgramme { Name = "7-Day Flow Plan", Description = "A structured week of movement, breathing and rest.", MinTier = 2 };
            db.Programmes.AddRange(move, dance, mobility, masters);

            // Public sample clips stand in for the client's real videos. Replace via the admin screens.
            move.Lessons.AddRange([
                L(1, "Full Body Flow", "A gentle flow to move, breathe and reconnect with your body.", "Yoga", 15, ClipA, preview: true),
                L(2, "Move & Release", "Release tension and feel lighter.", "Stretch & Mobility", 15, ClipB),
            ]);
            dance.Lessons.AddRange([
                L(1, "Dance with Joy", "Feel good movement for your mood.", "Dance", 60, ClipC, preview: true),
                L(2, "Barre Basics", "Build strength and grace at the barre.", "Barre & Pilates", 15, ClipA),
            ]);
            mobility.Lessons.AddRange([
                L(1, "Gentle Mobility", "Ease into stretching and mobility work.", "Stretch & Mobility", 15, ClipB),
                L(2, "6-Minute Reset", "A short guided meditation to breathe and reset.", "Meditation", 15, ClipA),
                L(3, "Strength Basics", "Build strength and stability with simple at-home moves.", "Strength & Conditioning", 15, ClipC),
                L(4, "Box Breathing", "A short breathing practice for focus and calm.", "Breathwork", 15, ClipB),
            ]);
            masters.Lessons.AddRange([
                L(1, "Day 1: Sunrise Flow", "A longer morning flow to start your day.", "Yoga", 15, ClipB),
                L(2, "Day 2: Dance Cardio Burn", "High energy dance for a full body workout.", "Dance", 60, ClipC),
            ]);
        }

        if (!await db.Classes.AnyAsync())
        {
            var start = DateTime.UtcNow.Date.AddDays(1).AddHours(7); // 09:00 SAST tomorrow
            string[] names = ["Morning Flow", "Dance with Joy", "Barre & Stretch", "Yoga Reset", "Weekend Wind-Down", "Mobility for Life"];
            string[] coaches = ["Deni", "Deni", "Deni", "Guest coach", "Deni", "Deni"];
            for (var i = 0; i < names.Length; i++)
            {
                var s = start.AddDays(i).AddHours(i % 3 * 5);
                db.Classes.Add(new ClassSession
                {
                    Name = names[i], Description = "All levels welcome. Bring water and a mat.", CoachName = coaches[i],
                    Location = "Rhythm & Flow Studio (placeholder)", StartTime = s, EndTime = s.AddMinutes(60), Capacity = 20,
                });
            }
        }
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Keeps the sample timetable from running dry while the studio's real classes have not been entered yet: whenever
    /// fewer than <paramref name="minimum"/> classes are coming up, more placeholder classes are added for the following
    /// days. It stops for good as soon as anyone creates a class of their own (a location without "(placeholder)").
    /// </summary>
    public static async Task EnsureUpcomingClassesAsync(AppDbContext db, int minimum = 5)
    {
        if (await db.Classes.AnyAsync(c => !c.Location.Contains("(placeholder)"))) return;

        var now = DateTime.UtcNow;
        var upcoming = await db.Classes.Where(c => c.Status == "SCHEDULED" && c.StartTime > now).Select(c => c.StartTime).ToListAsync();
        if (upcoming.Count >= minimum) return;

        string[] names = ["Morning Flow", "Dance with Joy", "Barre & Stretch", "Yoga Reset", "Weekend Wind-Down", "Mobility for Life"];
        var day = (upcoming.Count > 0 ? upcoming.Max() : now).Date.AddDays(1);
        var existing = await db.Classes.CountAsync();
        for (var i = upcoming.Count; i < minimum + 1; i++, day = day.AddDays(1))
        {
            var start = day.AddHours(7 + (existing + i) % 3 * 5);    // 09:00, 14:00 or 19:00 SAST
            db.Classes.Add(new ClassSession
            {
                Name = names[(existing + i) % names.Length], Description = "All levels welcome. Bring water and a mat.", CoachName = "Deni",
                Location = "Rhythm & Flow Studio (placeholder)", StartTime = start, EndTime = start.AddMinutes(60), Capacity = 20,
            });
        }
        await db.SaveChangesAsync();
    }

    private static Lesson L(int seq, string title, string desc, string cat, int ignoredSecs, (string Url, int Seconds) clip, bool preview = false) => new()
    {
        SequenceNumber = seq, Title = title, Description = desc, Category = cat, DurationSeconds = clip.Seconds,
        VideoProvider = "Remote", VideoReference = clip.Url, IsPreview = preview,
    };

    /// <summary>The name of the programme that holds the free practices from the client's YouTube channel.</summary>
    public const string FreePracticesName = "Free Practices";

    /// <summary>
    /// The client's public YouTube videos (channel "Rhythm &amp; Flow Wellness") as free previews: anyone with a free account can watch them,
    /// no subscription needed. Added once, to any database, and never again once the programme exists, so an administrator can hide or
    /// edit them without them coming back.
    /// </summary>
    public static async Task EnsureFreePracticesAsync(AppDbContext db)
    {
        if (await db.Programmes.AnyAsync(p => p.Name == FreePracticesName)) return;
        var programme = new FitnessProgramme
        {
            Name = FreePracticesName, MinTier = 1,
            Description = "Free practices from Rhythm & Flow's YouTube channel. Create a free account and press play, no membership needed.",
        };
        // id, title, category, seconds
        (string Id, string Title, string Category, int Seconds, string Description)[] videos =
        [
            ("NFixPh6y39U", "10 Minute Stretch and Mobility Morning Routine", "Stretch & Mobility", 596, "A morning routine for flexibility, circulation and mental clarity."),
            ("Z6zMtGnV_AI", "25 Minute Stretch Class", "Stretch & Mobility", 1715, "A stretch class to improve joint range of motion, posture and circulation."),
            ("JfLKk17S8KQ", "30 Minute Full Body Barre Workout", "Barre & Pilates", 1816, "A bodyweight barre workout blending ballet, Pilates, dance, yoga and strength, with beginner to advanced options."),
            ("Y3ZCfwIV3js", "30 Minute Full Body Sculpt Workout", "Strength & Conditioning", 1847, "A bodyweight strength workout with adjustable intensity and a good warm-up and cool-down."),
            ("mdyAXSmZx3w", "Mindfulness Monday: 15 Minute Mindfulness Check-In", "Meditation", 968, "A guided mind and body scan to come back to the present."),
            ("qHWZvPsLTEY", "Mindfulness Monday: 20 Minute Senses Meditation", "Meditation", 1230, "A guided meditation focusing on the senses."),
            ("0LyPiFSmLpA", "Mindfulness Monday: 10 Minute Balancing Breathwork", "Breathwork", 640, "A guided alternate nostril breathing practice to calm the mind and body."),
            ("hTkgxJc_w4Y", "Mindfulness Monday: 15 Minute Deep Belly Relaxation", "Breathwork", 930, "A guided relaxation using deep belly breaths."),
            ("S6qnP5641LM", "Mindfulness Monday: 15 Minute Breathwork Meditation", "Breathwork", 944, "A guided meditation connecting breath and body."),
        ];
        var n = 0;
        foreach (var v in videos)
            programme.Lessons.Add(new Lesson
            {
                Title = v.Title, Description = v.Description, Category = v.Category, Level = v.Id is "JfLKk17S8KQ" or "Y3ZCfwIV3js" ? "Intermediate" : "Beginner", DurationSeconds = v.Seconds,
                SequenceNumber = ++n, VideoProvider = RhythmFlow.Api.Services.LessonVideo.YouTube, VideoReference = v.Id, IsPreview = true,
                ThumbnailUrl = $"https://img.youtube.com/vi/{v.Id}/hqdefault.jpg",
            });
        db.Programmes.Add(programme);
        await db.SaveChangesAsync();
    }

    private static async Task SeedUsersAsync(AppDbContext db, IConfiguration cfg)
    {
        var hasher = new PasswordHasher<User>();
        foreach (var section in cfg.GetSection("Seed:Users").GetChildren())
        {
            var email = section["Email"]?.ToLowerInvariant();
            var password = section["Password"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) continue;
            if (await db.Users.AnyAsync(u => u.Email == email)) continue;
            var u = new User
            {
                FullName = section["FullName"] ?? "User", Username = section["Username"] ?? email.Split('@')[0],
                Email = email, Role = section["Role"] ?? Roles.Customer,
            };
            u.PasswordHash = hasher.HashPassword(u, password);
            db.Users.Add(u);
        }
    }
}

