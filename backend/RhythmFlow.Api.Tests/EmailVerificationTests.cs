using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RhythmFlow.Api.Controllers;
using RhythmFlow.Api.Data;
using RhythmFlow.Api.Domain;
using RhythmFlow.Api.Dtos;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Tests;

/// <summary>Signing up does not sign anyone in: they confirm their email with an emailed code first, then sign in.</summary>
public class EmailVerificationTests
{
    private static readonly IOptions<JwtOptions> Jwt = Options.Create(new JwtOptions { Key = new string('j', 40) });

    private static (AuthController auth, EmailVerificationService verify, FakeEmail mail) Build(TestDb t, bool required = true)
    {
        var mail = new FakeEmail();
        var verify = new EmailVerificationService(t.Db, mail, Jwt, NullLogger<EmailVerificationService>.Instance);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:RequireEmailVerification"] = required ? "true" : "false" }).Build();
        return (new AuthController(t.Db, new TokenService(Jwt), verify, cfg), verify, mail);
    }

    private static RegisterRequest Sign(string email = "new@example.com", string username = "newbie") =>
        new("New Person", username, email, "Password-123!");

    private static string CodeIn(FakeEmail mail, int index = -1) =>
        Regex.Match(mail.Sent[index < 0 ? mail.Sent.Count - 1 : index].Body, @"\b\d{6}\b").Value;

    [Fact]
    public async Task Signing_up_sends_a_code_and_does_not_sign_the_person_in()
    {
        using var t = new TestDb(); var (auth, _, mail) = Build(t);

        var result = await auth.Register(Sign());

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<RegisterResponse>(ok.Value);
        Assert.True(body.VerificationRequired); Assert.Equal("new@example.com", body.Email);
        Assert.False((await t.Db.Users.SingleAsync()).EmailVerified);
        var sent = Assert.Single(mail.Sent);
        Assert.Equal("new@example.com", sent.To); Assert.Matches(@"\b\d{6}\b", sent.Body);
        Assert.Contains("expires in 15 minutes", sent.Body);
    }

    [Fact]
    public async Task An_unverified_account_cannot_sign_in_even_with_the_right_password()
    {
        using var t = new TestDb(); var (auth, _, mail) = Build(t);
        await auth.Register(Sign());
        mail.Sent.Clear();

        var result = await auth.Login(new LoginRequest("newbie", "Password-123!"));

        var blocked = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(403, blocked.StatusCode);
        Assert.Contains("EMAIL_NOT_VERIFIED", System.Text.Json.JsonSerializer.Serialize(blocked.Value));
        Assert.Empty(mail.Sent);   // a code is still live from sign-up, so no second email
    }

    [Fact]
    public async Task A_wrong_password_on_an_unverified_account_gives_the_normal_error_and_sends_nothing()
    {
        using var t = new TestDb(); var (auth, _, mail) = Build(t);
        await auth.Register(Sign());
        mail.Sent.Clear();

        var result = await auth.Login(new LoginRequest("newbie", "wrong-password"));

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Empty(mail.Sent);
    }

    [Fact]
    public async Task The_emailed_code_confirms_the_email_and_then_the_person_can_sign_in()
    {
        using var t = new TestDb(); var (auth, _, mail) = Build(t);
        await auth.Register(Sign());

        var verified = await auth.VerifyEmail(new VerifyEmailRequest("new@example.com", CodeIn(mail)));

        Assert.IsType<OkObjectResult>(verified);
        Assert.True((await t.Db.Users.SingleAsync()).EmailVerified);
        var login = await auth.Login(new LoginRequest("newbie", "Password-123!"));
        var ok = Assert.IsType<OkObjectResult>(login.Result);
        Assert.False(string.IsNullOrEmpty(Assert.IsType<AuthResponse>(ok.Value).Token));
    }

    [Fact]
    public async Task A_wrong_code_is_refused_and_five_wrong_guesses_lock_that_code()
    {
        using var t = new TestDb(); var (auth, verify, mail) = Build(t);
        await auth.Register(Sign());
        var real = CodeIn(mail);
        var wrong = real == "000000" ? "111111" : "000000";

        for (var i = 0; i < 5; i++) Assert.False((await verify.VerifyAsync("new@example.com", wrong)).Ok);
        var late = await verify.VerifyAsync("new@example.com", real);   // right code, but the guesses are used up

        Assert.False(late.Ok);
        Assert.False((await t.Db.Users.SingleAsync()).EmailVerified);
    }

    [Fact]
    public async Task A_code_that_has_expired_is_refused_and_a_new_one_works()
    {
        using var t = new TestDb(); var (auth, verify, mail) = Build(t);
        await auth.Register(Sign());
        var old = CodeIn(mail);
        foreach (var c in t.Db.PasswordResets) c.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await t.Db.SaveChangesAsync();
        Assert.False((await verify.VerifyAsync("new@example.com", old)).Ok);

        await verify.ResendAsync("new@example.com");

        Assert.True((await verify.VerifyAsync("new@example.com", CodeIn(mail))).Ok);
    }

    [Fact]
    public async Task Only_the_newest_code_works_and_codes_are_limited_to_three_an_hour()
    {
        using var t = new TestDb(); var (auth, verify, mail) = Build(t);
        await auth.Register(Sign());                       // code 1
        await verify.ResendAsync("new@example.com");       // code 2
        await verify.ResendAsync("new@example.com");       // code 3
        await verify.ResendAsync("new@example.com");       // refused: 3 already this hour
        Assert.Equal(3, mail.Sent.Count);

        Assert.False((await verify.VerifyAsync("new@example.com", CodeIn(mail, 0))).Ok);   // an older code
        Assert.True((await verify.VerifyAsync("new@example.com", CodeIn(mail, 2))).Ok);
    }

    [Fact]
    public async Task Asking_for_a_code_gives_the_same_answer_for_unknown_and_already_verified_emails_and_sends_nothing()
    {
        using var t = new TestDb(); var (auth, _, mail) = Build(t);
        var verified = t.AddUser();

        var a = await auth.ResendVerification(new ResendVerificationRequest("nobody@example.com"));
        var b = await auth.ResendVerification(new ResendVerificationRequest(verified.Email));

        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(((OkObjectResult)a).Value), System.Text.Json.JsonSerializer.Serialize(((OkObjectResult)b).Value));
        Assert.Empty(mail.Sent);
    }

    [Fact]
    public async Task A_password_reset_code_cannot_verify_an_email_and_a_verification_code_cannot_reset_a_password()
    {
        using var t = new TestDb(); var (auth, verify, mail) = Build(t);
        await auth.Register(Sign());
        var verifyCode = CodeIn(mail);
        var reset = new PasswordResetService(t.Db, mail, Jwt, NullLogger<PasswordResetService>.Instance);

        await reset.RequestAsync("new@example.com");
        var resetCode = CodeIn(mail);

        Assert.False((await verify.VerifyAsync("new@example.com", resetCode)).Ok);
        Assert.False((await reset.ResetAsync("new@example.com", verifyCode, "Another-Pass-1!")).Ok);
    }

    [Fact]
    public async Task Resetting_the_password_with_an_emailed_code_also_confirms_the_email()
    {
        using var t = new TestDb(); var (auth, _, mail) = Build(t);
        await auth.Register(Sign());
        var reset = new PasswordResetService(t.Db, mail, Jwt, NullLogger<PasswordResetService>.Instance);
        await reset.RequestAsync("new@example.com");

        Assert.True((await reset.ResetAsync("new@example.com", CodeIn(mail), "Another-Pass-1!")).Ok);

        Assert.True((await t.Db.Users.SingleAsync()).EmailVerified);
    }

    [Fact]
    public async Task Accounts_that_existed_before_verification_still_sign_in()
    {
        using var t = new TestDb(); var (auth, _, _) = Build(t);
        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
        var old = new User { FullName = "Old Member", Username = "oldie", Email = "old@example.com" };   // EmailVerified defaults to true
        old.PasswordHash = hasher.HashPassword(old, "Password-123!");
        t.Db.Users.Add(old); await t.Db.SaveChangesAsync();

        var login = await auth.Login(new LoginRequest("old@example.com", "Password-123!"));

        Assert.IsType<OkObjectResult>(login.Result);
    }

    [Fact]
    public async Task A_server_with_no_email_set_up_can_switch_verification_off_and_the_person_just_signs_in_afterwards()
    {
        using var t = new TestDb(); var (auth, _, mail) = Build(t, required: false);

        var result = await auth.Register(Sign());

        var body = Assert.IsType<RegisterResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.False(body.VerificationRequired);
        Assert.Empty(mail.Sent);
        Assert.IsType<OkObjectResult>((await auth.Login(new LoginRequest("newbie", "Password-123!"))).Result);
    }

    [Fact]
    public async Task Sign_ups_that_never_confirm_are_removed_after_a_week_and_confirmed_or_older_accounts_stay()
    {
        using var t = new TestDb(); var (auth, verify, _) = Build(t);
        await auth.Register(Sign("stale@example.com", "stale"));
        await auth.Register(Sign("fresh@example.com", "fresh"));
        var kept = t.AddUser();
        var stale = await t.Db.Users.SingleAsync(u => u.Username == "stale");
        stale.CreatedAt = DateTime.UtcNow.AddDays(-8); await t.Db.SaveChangesAsync();

        var removed = await verify.PurgeUnverifiedAsync(DateTime.UtcNow);

        Assert.Equal(1, removed);
        Assert.Equal(["fresh", kept.Username], (await t.Db.Users.Select(u => u.Username).ToListAsync()).OrderBy(x => x == "fresh" ? 0 : 1).ToArray());
        Assert.DoesNotContain(t.Db.PasswordResets, c => c.UserId == stale.Id);
    }

    [Fact]
    public async Task Duplicate_emails_and_usernames_are_still_refused_at_sign_up()
    {
        using var t = new TestDb(); var (auth, _, _) = Build(t);
        await auth.Register(Sign());

        Assert.IsType<ConflictObjectResult>((await auth.Register(Sign(username: "other"))).Result);
        Assert.IsType<ConflictObjectResult>((await auth.Register(Sign(email: "other@example.com"))).Result);
    }

    [Fact]
    public async Task A_database_from_before_verification_gets_the_new_columns_and_everyone_in_it_counts_as_verified()
    {
        using var t = new TestDb();
        t.AddUser();
        await t.Db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Users\" DROP COLUMN \"EmailVerified\"");
        await t.Db.Database.ExecuteSqlRawAsync("ALTER TABLE \"PasswordResets\" DROP COLUMN \"Purpose\"");

        await SchemaUpgrades.ApplyAsync(t.Db);

        Assert.True((await t.Db.Users.AsNoTracking().SingleAsync()).EmailVerified);
    }
}
