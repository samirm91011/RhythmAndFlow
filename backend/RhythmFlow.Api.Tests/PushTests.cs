using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

public class FakePush : IPushSender
{
    public bool Enabled { get; set; } = true;
    public bool Throws { get; set; }
    public HashSet<string> InvalidTokens { get; } = new();
    public List<(IReadOnlyList<string> Tokens, PushMessage Message)> Sent { get; } = new();

    public Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken ct = default)
    {
        if (Throws) throw new InvalidOperationException("Firebase is down");
        Sent.Add((tokens, message));
        return Task.FromResult<IReadOnlyList<string>>(tokens.Where(InvalidTokens.Contains).ToList());
    }
}

public class PushTests
{
    private const string TokenA = "token-aaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string TokenB = "token-bbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private static NotificationService Build(TestDb t, FakePush push) => new(t.Db, push, NullLogger<NotificationService>.Instance);

    [Fact]
    public async Task A_notification_is_pushed_to_every_phone_of_that_user_only()
    {
        using var t = new TestDb();
        var push = new FakePush();
        var svc = Build(t, push);
        var me = t.AddUser();
        var other = t.AddUser();
        await svc.RegisterTokenAsync(me.Id, TokenA);
        await svc.RegisterTokenAsync(me.Id, TokenB);
        await svc.RegisterTokenAsync(other.Id, "token-cccccccccccccccccccccccccccc");

        await svc.AddAsync(me.Id, "PAYMENT", "You're subscribed!", "Enjoy", "subscription");

        var sent = Assert.Single(push.Sent);
        Assert.Equal(new[] { TokenA, TokenB }, sent.Tokens.OrderBy(x => x));
        Assert.Equal("You're subscribed!", sent.Message.Title);
        Assert.Equal("subscription", sent.Message.Route);
        Assert.Equal(await t.Db.Notifications.Select(n => n.Id).SingleAsync(), sent.Message.NotificationId);
    }

    [Fact]
    public async Task Nothing_is_sent_when_the_user_has_no_registered_phone()
    {
        using var t = new TestDb();
        var push = new FakePush();
        await Build(t, push).AddAsync(t.AddUser().Id, "INFO", "Hi", "There");
        Assert.Empty(push.Sent);
    }

    [Fact]
    public async Task Nothing_is_sent_when_push_is_switched_off_but_the_in_app_notification_is_saved()
    {
        using var t = new TestDb();
        var push = new FakePush { Enabled = false };
        var svc = Build(t, push);
        var u = t.AddUser();
        await svc.RegisterTokenAsync(u.Id, TokenA);

        await svc.AddAsync(u.Id, "INFO", "Hi", "There");

        Assert.Empty(push.Sent);
        Assert.Single(t.Db.Notifications);
    }

    [Fact]
    public async Task A_push_failure_does_not_lose_the_in_app_notification_or_throw()
    {
        using var t = new TestDb();
        var push = new FakePush { Throws = true };
        var svc = Build(t, push);
        var u = t.AddUser();
        await svc.RegisterTokenAsync(u.Id, TokenA);

        await svc.AddAsync(u.Id, "INFO", "Hi", "There");

        Assert.Single(t.Db.Notifications);
    }

    [Fact]
    public async Task Tokens_that_Firebase_reports_as_invalid_are_forgotten()
    {
        using var t = new TestDb();
        var push = new FakePush();
        push.InvalidTokens.Add(TokenA);
        var svc = Build(t, push);
        var u = t.AddUser();
        await svc.RegisterTokenAsync(u.Id, TokenA);
        await svc.RegisterTokenAsync(u.Id, TokenB);

        await svc.AddAsync(u.Id, "INFO", "Hi", "There");

        Assert.Equal(new[] { TokenB }, await t.Db.DeviceTokens.Select(x => x.Token).ToListAsync());
    }

    [Fact]
    public async Task Registering_the_same_phone_twice_keeps_one_row()
    {
        using var t = new TestDb();
        var svc = Build(t, new FakePush());
        var u = t.AddUser();

        await svc.RegisterTokenAsync(u.Id, TokenA);
        await svc.RegisterTokenAsync(u.Id, TokenA);

        Assert.Single(t.Db.DeviceTokens);
    }

    [Fact]
    public async Task A_phone_that_signs_in_as_someone_else_moves_to_the_new_person()
    {
        using var t = new TestDb();
        var push = new FakePush();
        var svc = Build(t, push);
        var first = t.AddUser();
        var second = t.AddUser();
        await svc.RegisterTokenAsync(first.Id, TokenA);
        await svc.RegisterTokenAsync(second.Id, TokenA);

        await svc.AddAsync(first.Id, "INFO", "For the first person", "x");

        Assert.Empty(push.Sent);
        Assert.Equal(second.Id, (await t.Db.DeviceTokens.SingleAsync()).UserId);
    }

    [Fact]
    public async Task Removing_a_phone_stops_pushes_to_it()
    {
        using var t = new TestDb();
        var push = new FakePush();
        var svc = Build(t, push);
        var u = t.AddUser();
        await svc.RegisterTokenAsync(u.Id, TokenA);

        await svc.RemoveTokenAsync(u.Id, TokenA);
        await svc.AddAsync(u.Id, "INFO", "Hi", "There");

        Assert.Empty(push.Sent);
    }

    [Fact]
    public async Task A_user_cannot_remove_another_users_phone()
    {
        using var t = new TestDb();
        var svc = Build(t, new FakePush());
        var owner = t.AddUser();
        var stranger = t.AddUser();
        await svc.RegisterTokenAsync(owner.Id, TokenA);

        await svc.RemoveTokenAsync(stranger.Id, TokenA);

        Assert.Single(t.Db.DeviceTokens);
    }

    [Fact]
    public async Task Only_the_five_most_recent_phones_are_kept()
    {
        using var t = new TestDb();
        var svc = Build(t, new FakePush());
        var u = t.AddUser();
        for (var i = 0; i < 7; i++)
        {
            await svc.RegisterTokenAsync(u.Id, $"token-{i:D2}-xxxxxxxxxxxxxxxxxxxxxxxx");
            await Task.Delay(5);
        }

        Assert.Equal(5, await t.Db.DeviceTokens.CountAsync());
        Assert.DoesNotContain(t.Db.DeviceTokens, x => x.Token.StartsWith("token-00") || x.Token.StartsWith("token-01"));
    }

    [Fact]
    public async Task Admin_notifications_are_pushed_to_each_administrator()
    {
        using var t = new TestDb();
        var push = new FakePush();
        var svc = Build(t, push);
        var admin1 = t.AddUser(role: Roles.Admin);
        var admin2 = t.AddUser(role: Roles.Admin);
        t.AddUser(); // a customer, who must not get it
        await svc.RegisterTokenAsync(admin1.Id, TokenA);
        await svc.RegisterTokenAsync(admin2.Id, TokenB);

        await svc.AddForAdminsAsync("ADMIN_ERROR", "Error", "Something broke", "admin/errors");

        Assert.Equal(2, push.Sent.Count);
        Assert.All(push.Sent, s => Assert.Equal("admin/errors", s.Message.Route));
    }

    [Fact]
    public async Task Staged_notifications_are_pushed_only_after_PushStagedAsync()
    {
        using var t = new TestDb();
        var push = new FakePush();
        var svc = Build(t, push);
        var u = t.AddUser();
        await svc.RegisterTokenAsync(u.Id, TokenA);

        svc.Stage(new[] { u.Id }, "CLASS", "Class cancelled", "Sorry", "classes");
        await t.Db.SaveChangesAsync();
        Assert.Empty(push.Sent);

        await svc.PushStagedAsync();
        Assert.Single(push.Sent);
        await svc.PushStagedAsync();           // a second call must not send the same thing again
        Assert.Single(push.Sent);
    }
}
