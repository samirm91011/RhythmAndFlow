using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Domain;

namespace RhythmFlow.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<SubscriptionPlan> Plans => Set<SubscriptionPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<FitnessProgramme> Programmes => Set<FitnessProgramme>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Progress> Progress => Set<Progress>();
    public DbSet<ClassSession> Classes => Set<ClassSession>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<JournalEntry> Journal => Set<JournalEntry>();
    public DbSet<PasswordResetCode> PasswordResets => Set<PasswordResetCode>();
    public DbSet<AppNotification> Notifications => Set<AppNotification>();
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();

    // Store and read every timestamp as UTC so the API always serialises them with a trailing "Z".
    protected override void ConfigureConventions(ModelConfigurationBuilder c)
    {
        c.Properties<DateTime>().HaveConversion<UtcConverter>();
        c.Properties<DateTime?>().HaveConversion<NullableUtcConverter>();
    }

    private class UtcConverter() : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
        v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private class NullableUtcConverter() : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
        v => v.HasValue ? v.Value.ToUniversalTime() : v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(100);
            e.Property(x => x.Email).HasMaxLength(255);
            e.Property(x => x.Username).HasMaxLength(50);
            e.Property(x => x.PasswordHash).HasMaxLength(255);
            e.Property(x => x.Role).HasMaxLength(20);
            e.Property(x => x.AccountStatus).HasMaxLength(20);
        });
        b.Entity<SubscriptionPlan>().Property(x => x.Price).HasPrecision(10, 2);
        b.Entity<Subscription>(e =>
        {
            e.Property(x => x.Status).HasMaxLength(20);
            e.HasIndex(x => x.UserId);
        });
        b.Entity<Payment>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(10, 2);
            e.HasIndex(x => x.TransactionReference).IsUnique();
        });
        b.Entity<Progress>(e =>
        {
            e.Property(x => x.CompletionPercentage).HasPrecision(5, 2);
            e.HasIndex(x => new { x.UserId, x.LessonId }).IsUnique();
        });
        b.Entity<Booking>(e =>
        {
            e.Property(x => x.Status).HasMaxLength(20);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.ClassId);
            // A user can hold at most one live booking per class, even if two requests arrive at the same moment.
            e.HasIndex(x => new { x.UserId, x.ClassId }).IsUnique().HasFilter("\"Status\" = 'BOOKED'");
        });
        b.Entity<PasswordResetCode>().HasIndex(x => new { x.UserId, x.CreatedAt });
        b.Entity<AppNotification>().HasIndex(x => new { x.UserId, x.CreatedAt });
        b.Entity<DeviceToken>(e =>
        {
            e.Property(x => x.Token).HasMaxLength(512);
            e.Property(x => x.Platform).HasMaxLength(20);
            e.HasIndex(x => x.Token).IsUnique();
            e.HasIndex(x => x.UserId);
        });
        b.Entity<ErrorLog>(e =>
        {
            e.HasIndex(x => x.Fingerprint);
            e.HasIndex(x => x.LastSeen);
        });
    }
}
