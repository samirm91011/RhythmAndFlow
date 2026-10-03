using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RhythmFlow.Api.Dtos;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account")]
public class AccountController(AccountService account) : ApiController
{
    /// <summary>A copy of everything we hold about the signed-in person, as JSON.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var data = await account.ExportAsync(UserId);
        if (data is null) return Unauthorized();
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        return File(Encoding.UTF8.GetBytes(json), "application/json", "rhythm-and-flow-my-data.json");
    }

    /// <summary>Deletes the account after re-checking the password. A POST so the password stays out of the URL.</summary>
    [HttpPost("delete"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Delete(DeleteAccountRequest req)
    {
        var (ok, error) = await account.DeleteAsync(UserId, req.Password);
        return ok ? Ok(new { message = "Your account and personal data have been deleted." }) : BadRequest(new { error });
    }
}

/// <summary>Public Terms of Use and Privacy Policy pages (the app opens these in the browser).</summary>
[ApiController]
[AllowAnonymous]
public class LegalController : ControllerBase
{
    [HttpGet("/terms")]
    public ContentResult Terms() => Page("Terms of Use", """
        <p><b>Draft for review.</b> These terms must be reviewed and approved by the business before launch.</p>
        <h2>1. Who we are</h2>
        <p>Rhythm &amp; Flow ("we", "us") offers movement and wellbeing videos, live classes and journaling tools through this app.</p>
        <h2>2. Your account</h2>
        <p>You must give accurate details and keep your password private. You are responsible for activity on your account.</p>
        <h2>3. Subscriptions and payments</h2>
        <p>Paid plans renew every month until you cancel. Payments are handled by PayFast, and we never see or store your card details. When you cancel, you keep access until the end of the period you have paid for. Fees already paid are not refunded unless the law requires it.</p>
        <h2>4. Content and fair use</h2>
        <p>Videos and programmes are for your personal use only. You may not copy, record, share or resell them.</p>
        <h2>5. Classes and health</h2>
        <p>Exercise carries some risk. Speak to a health professional before starting a programme, and stop if something hurts. You take part at your own risk.</p>
        <h2>6. Bookings</h2>
        <p>You can cancel a class booking up to two hours before it starts. We may cancel a class and will tell you if we do.</p>
        <h2>7. Ending your account</h2>
        <p>You can delete your account in the app under Settings. We may suspend accounts that break these terms.</p>
        <h2>8. Changes and contact</h2>
        <p>We may update these terms and will tell you about important changes. Contact: [business e-mail address].</p>
        """);

    [HttpGet("/privacy")]
    public ContentResult Privacy() => Page("Privacy Policy", """
        <p><b>Draft for review.</b> This policy must be reviewed and approved by the business before launch.</p>
        <h2>What we collect</h2>
        <p>Your name, username, e-mail address and password (stored only as a one-way hash), your subscription and payment status, your class bookings, your viewing progress, your journal entries and mood check-ins, and technical error reports from the app.</p>
        <h2>Why we collect it</h2>
        <p>To run your account, give you access to the content you have paid for, take bookings, send reminders and notifications, and keep the app working.</p>
        <h2>Who we share it with</h2>
        <p>PayFast (payments), our hosting provider, and our e-mail provider (for password reset codes and alerts). We do not sell your information.</p>
        <h2>How long we keep it</h2>
        <p>For as long as you have an account. Payment records are kept without your personal details after you delete your account, because we must keep financial records.</p>
        <h2>Your rights</h2>
        <p>Under POPIA you can see, correct and delete your information. In the app, go to Settings to download a copy of your data or delete your account.</p>
        <h2>Security</h2>
        <p>Traffic is encrypted, passwords are hashed, and videos are only available through short-lived, signed links.</p>
        <h2>Contact</h2>
        <p>[business name and e-mail address of the responsible person].</p>
        """);

    private ContentResult Page(string title, string body) => new()
    {
        ContentType = "text/html; charset=utf-8",
        Content = $$"""
            <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{title}} - Rhythm &amp; Flow</title>
            <style>body{font-family:system-ui,sans-serif;max-width:680px;margin:0 auto;padding:24px 16px;line-height:1.55;color:#1b1b1b}
            h1{font-family:Georgia,serif;color:#2f7a6e}h2{font-size:1.05rem;margin-top:1.6em}</style></head>
            <body><h1>{{title}}</h1>{{body}}</body></html>
            """,
    };
}
