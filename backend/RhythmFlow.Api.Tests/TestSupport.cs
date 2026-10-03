using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

/// <summary>A throwaway in-memory SQLite database with the real schema, one per test.</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    public AppDbContext Db { get; }

    public TestDb()
    {
        _conn.Open();
        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
        Db.Database.EnsureCreated();
    }

    public User AddUser(string name = "Test User", string role = Roles.Customer)
    {
        var u = new User { FullName = name, Username = name.Replace(' ', '_') + Guid.NewGuid().ToString("N")[..4], Email = $"{Guid.NewGuid():N}@example.com", Role = role };
        Db.Users.Add(u);
        Db.SaveChanges();
        return u;
    }

    public SubscriptionPlan AddPlan(int tier = 1, decimal price = 99)
    {
        var p = new SubscriptionPlan { Name = $"Plan {tier}", Price = price, Tier = tier };
        Db.Plans.Add(p);
        Db.SaveChanges();
        return p;
    }

    public Lesson AddLesson(int minTier = 1, bool preview = false, int seconds = 100)
    {
        var prog = new FitnessProgramme { Name = "Programme", MinTier = minTier };
        var lesson = new Lesson { Title = "Lesson", DurationSeconds = seconds, IsPreview = preview, Programme = prog };
        Db.Lessons.Add(lesson);
        Db.SaveChanges();
        return lesson;
    }

    public ClassSession AddClass(int capacity = 10, TimeSpan? startsIn = null)
    {
        var start = DateTime.UtcNow.Add(startsIn ?? TimeSpan.FromDays(2));
        var c = new ClassSession { Name = "Morning Flow", CoachName = "Deni", Location = "Studio", StartTime = start, EndTime = start.AddHours(1), Capacity = capacity };
        Db.Classes.Add(c);
        Db.SaveChanges();
        return c;
    }

    public Subscription AddSubscription(User user, SubscriptionPlan plan, string status, DateTime? end = null, string? token = null)
    {
        var s = new Subscription { UserId = user.Id, PlanId = plan.Id, Status = status, StartDate = DateTime.UtcNow.AddDays(-5), EndDate = end, PayFastReference = token };
        Db.Subscriptions.Add(s);
        Db.SaveChanges();
        return s;
    }

    public void Dispose() { Db.Dispose(); _conn.Dispose(); }
}

public class FakeEmail : IEmailSender
{
    public List<(string To, string Subject, string Body)> Sent { get; } = new();
    public Task<bool> SendAsync(string to, string subject, string body) { Sent.Add((to, subject, body)); return Task.FromResult(true); }
}

public class FakePayFastApi : IPayFastApi
{
    public bool CanCall { get; set; } = true;
    public bool Succeeds { get; set; } = true;
    public string? LastToken { get; private set; }
    public int Calls { get; private set; }
    public Task<(bool Ok, string Message)> CancelSubscriptionAsync(string token)
    {
        Calls++; LastToken = token;
        return Task.FromResult((Succeeds, Succeeds ? "ok" : "PayFast could not cancel this subscription (error 500)."));
    }
}
