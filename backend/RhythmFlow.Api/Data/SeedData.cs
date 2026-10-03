using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Domain;

namespace RhythmFlow.Api.Data;

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
            db.Plans.AddRange(
                new SubscriptionPlan
                {
                    Name = "Flow", Price = 99, Tier = 1, Description = "Start moving with the essentials.",
                    Features = "Move & Release programme|Dance with Joy programme|Progress tracking|Class booking",
                },
                new SubscriptionPlan
                {
                    Name = "Rhythm", Price = 199, Tier = 2, Description = "Everything in Flow plus deeper practices.",
                    Features = "Everything in Flow|Mindful Mobility programme|Meditation library|Priority class booking",
                },
                new SubscriptionPlan
                {
                    Name = "Rhythm & Flow Unlimited", Price = 299, Tier = 3, Description = "The full Rhythm & Flow experience.",
                    Features = "Everything in Rhythm|Full Body Flow masterclasses|New content first|Mentorship Q&A access",
                });
        }

        if (!await db.Programmes.AnyAsync())
        {
            var move = new FitnessProgramme { Name = "Move & Release", Description = "Gentle practices to release tension and feel lighter.", MinTier = 1 };
            var dance = new FitnessProgramme { Name = "Dance with Joy", Description = "Feel-good dance movement for every level.", MinTier = 1 };
            var mobility = new FitnessProgramme { Name = "Mindful Mobility", Description = "Stretch, restore and move well for life.", MinTier = 2 };
            var masters = new FitnessProgramme { Name = "Full Body Flow Masterclass", Description = "Longer, deeper flows with Deni.", MinTier = 3 };
            db.Programmes.AddRange(move, dance, mobility, masters);

            // Public sample clips stand in for the client's real videos. Replace via the admin screens.
            move.Lessons.AddRange([
                L(1, "Full Body Flow", "A gentle flow to move, breathe and reconnect with your body.", "Yoga", 15, ClipA, preview: true),
                L(2, "Move & Release", "Release tension and feel lighter.", "Stretch", 15, ClipB),
            ]);
            dance.Lessons.AddRange([
                L(1, "Dance with Joy", "Feel good movement for your mood.", "Dance", 60, ClipC, preview: true),
                L(2, "Barre Basics", "Build strength and grace at the barre.", "Barre", 15, ClipA),
            ]);
            mobility.Lessons.AddRange([
                L(1, "Gentle Mobility", "Ease into stretching and mobility work.", "Stretch", 15, ClipB),
                L(2, "6-Minute Reset", "A short guided meditation to breathe and reset.", "Meditation", 15, ClipA),
            ]);
            masters.Lessons.AddRange([
                L(1, "Sunrise Flow", "A longer morning flow to start your day.", "Yoga", 15, ClipB),
                L(2, "Dance Cardio Burn", "High energy dance for a full body workout.", "Dance", 60, ClipC),
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

    private static Lesson L(int seq, string title, string desc, string cat, int ignoredSecs, (string Url, int Seconds) clip, bool preview = false) => new()
    {
        SequenceNumber = seq, Title = title, Description = desc, Category = cat, DurationSeconds = clip.Seconds,
        VideoProvider = "Remote", VideoReference = clip.Url, IsPreview = preview,
    };

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

