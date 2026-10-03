using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

public class AccountTests
{
    private const string Password = "CorrectHorse1";

    private static (AccountService svc, FakePayFastApi api) Build(TestDb t)
    {
        var api = new FakePayFastApi();
        var payFast = new PayFastService(Options.Create(new PayFastOptions { MerchantId = "1", MerchantKey = "k", Passphrase = "p" }));
        var subs = new SubscriptionService(t.Db, payFast, api, new NotificationService(t.Db), NullLogger<SubscriptionService>.Instance);
        return (new AccountService(t.Db, subs), api);
    }

    private static User UserWithPassword(TestDb t, string role = Roles.Customer)
    {
        var u = t.AddUser(role: role);
        u.PasswordHash = new PasswordHasher<User>().HashPassword(u, Password);
        t.Db.SaveChanges();
        return u;
    }

    [Fact]
    public async Task Delete_with_wrong_password_changes_nothing()
    {
        using var t = new TestDb();
        var (svc, _) = Build(t);
        var u = UserWithPassword(t);

        var (ok, error) = await svc.DeleteAsync(u.Id, "wrong-password");

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Equal("ACTIVE", (await t.Db.Users.FindAsync(u.Id))!.AccountStatus);
    }

    [Fact]
    public async Task Delete_removes_personal_content_and_the_user()
    {
        using var t = new TestDb();
        var (svc, _) = Build(t);
        var u = UserWithPassword(t);
        var cls = t.AddClass();
        t.Db.Bookings.Add(new Booking { UserId = u.Id, ClassId = cls.Id });
        t.Db.Journal.Add(new JournalEntry { UserId = u.Id, Text = "private thoughts" });
        t.Db.Notifications.Add(new AppNotification { UserId = u.Id, Title = "Hi" });
        await t.Db.SaveChangesAsync();

        var (ok, _) = await svc.DeleteAsync(u.Id, Password);

        Assert.True(ok);
        Assert.Null(await t.Db.Users.FindAsync(u.Id));
        Assert.Empty(t.Db.Journal);
        Assert.Empty(t.Db.Bookings);
        Assert.Empty(t.Db.Notifications);
    }

    [Fact]
    public async Task Delete_keeps_payment_records_but_anonymises_the_person()
    {
        using var t = new TestDb();
        var (svc, _) = Build(t);
        var u = UserWithPassword(t);
        var sub = t.AddSubscription(u, t.AddPlan(), SubscriptionStatus.Expired);
        t.Db.Payments.Add(new Payment { SubscriptionId = sub.Id, Amount = 99, Status = "COMPLETE" });
        await t.Db.SaveChangesAsync();

        var (ok, _) = await svc.DeleteAsync(u.Id, Password);

        Assert.True(ok);
        var kept = await t.Db.Users.FindAsync(u.Id);
        Assert.NotNull(kept);
        Assert.Equal("DELETED", kept!.AccountStatus);
        Assert.Equal("Deleted user", kept.FullName);
        Assert.EndsWith("@deleted.invalid", kept.Email);
        Assert.Equal("", kept.PasswordHash);
        Assert.Single(t.Db.Payments);
    }

    [Fact]
    public async Task Delete_cancels_an_active_PayFast_subscription_first()
    {
        using var t = new TestDb();
        var (svc, api) = Build(t);
        var u = UserWithPassword(t);
        t.AddSubscription(u, t.AddPlan(), SubscriptionStatus.Active, DateTime.UtcNow.AddDays(20), token: "pf-token-1");

        var (ok, _) = await svc.DeleteAsync(u.Id, Password);

        Assert.True(ok);
        Assert.Equal(1, api.Calls);
        Assert.Equal("pf-token-1", api.LastToken);
    }

    [Fact]
    public async Task Delete_is_refused_and_nothing_is_removed_when_PayFast_cancel_fails()
    {
        using var t = new TestDb();
        var (svc, api) = Build(t);
        api.Succeeds = false;
        var u = UserWithPassword(t);
        t.AddSubscription(u, t.AddPlan(), SubscriptionStatus.Active, DateTime.UtcNow.AddDays(20), token: "pf-token-1");
        t.Db.Journal.Add(new JournalEntry { UserId = u.Id, Text = "keep me" });
        await t.Db.SaveChangesAsync();

        var (ok, error) = await svc.DeleteAsync(u.Id, Password);

        Assert.False(ok);
        Assert.Contains("PayFast", error);
        Assert.Equal("ACTIVE", (await t.Db.Users.FindAsync(u.Id))!.AccountStatus);
        Assert.Single(t.Db.Journal);
    }

    [Fact]
    public async Task The_only_administrator_cannot_be_deleted()
    {
        using var t = new TestDb();
        var (svc, _) = Build(t);
        var admin = UserWithPassword(t, Roles.Admin);

        var (ok, error) = await svc.DeleteAsync(admin.Id, Password);

        Assert.False(ok);
        Assert.Contains("only administrator", error);
    }

    [Fact]
    public async Task Another_administrator_can_be_deleted_when_a_second_one_exists()
    {
        using var t = new TestDb();
        var (svc, _) = Build(t);
        UserWithPassword(t, Roles.Admin);
        var second = UserWithPassword(t, Roles.Admin);

        var (ok, _) = await svc.DeleteAsync(second.Id, Password);

        Assert.True(ok);
    }

    [Fact]
    public async Task Delete_clears_the_user_from_error_logs()
    {
        using var t = new TestDb();
        var (svc, _) = Build(t);
        var u = UserWithPassword(t);
        t.Db.ErrorLogs.Add(new ErrorLog { Message = "boom", UserId = u.Id, UserEmail = u.Email });
        await t.Db.SaveChangesAsync();

        await svc.DeleteAsync(u.Id, Password);

        var log = await t.Db.ErrorLogs.SingleAsync();
        Assert.Null(log.UserId);
        Assert.Null(log.UserEmail);
    }

    [Fact]
    public async Task Export_contains_only_the_requesting_users_data()
    {
        using var t = new TestDb();
        var (svc, _) = Build(t);
        var me = UserWithPassword(t);
        var other = UserWithPassword(t);
        t.Db.Journal.Add(new JournalEntry { UserId = me.Id, Text = "mine" });
        t.Db.Journal.Add(new JournalEntry { UserId = other.Id, Text = "theirs" });
        await t.Db.SaveChangesAsync();

        var json = JsonSerializer.Serialize(await svc.ExportAsync(me.Id));

        Assert.Contains("mine", json);
        Assert.DoesNotContain("theirs", json);
        Assert.Contains(me.Email, json);
        Assert.DoesNotContain(other.Email, json);
        Assert.DoesNotContain("PasswordHash", json);
    }
}
