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

/// <summary>
/// The Terms of Use and Privacy Policy are published on the client's website. These short links send people there, so
/// there is only one version of each document. The addresses can be changed with Legal:TermsUrl and Legal:PrivacyUrl.
/// </summary>
[ApiController]
[AllowAnonymous]
public class LegalController(IConfiguration cfg) : ControllerBase
{
    private const string DefaultTerms = "https://rhythmandflow.co.za/terms-of-use/";
    private const string DefaultPrivacy = "https://rhythmandflow.co.za/privacy-policy/";

    [HttpGet("/terms")]
    public IActionResult Terms() => Redirect(cfg["Legal:TermsUrl"] is { Length: > 0 } u ? u : DefaultTerms);

    [HttpGet("/privacy")]
    public IActionResult Privacy() => Redirect(cfg["Legal:PrivacyUrl"] is { Length: > 0 } u ? u : DefaultPrivacy);
}
