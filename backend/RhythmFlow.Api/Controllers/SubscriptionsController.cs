using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RhythmFlow.Api.Dtos;
using RhythmFlow.Api.Services;

namespace RhythmFlow.Api.Controllers;

[ApiController, Authorize]
[Route("api/subscriptions")]
public class SubscriptionsController(SubscriptionService subs) : ApiController
{
    [HttpGet]
    public Task<List<SubscriptionDto>> Mine() => subs.MineAsync(UserId);

    /// <summary>Creates a pending subscription and returns the hosted PayFast checkout link (FR-05, FR-06).</summary>
    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutResponse>> Checkout(CheckoutRequest req)
    {
        try { return await subs.CheckoutAsync(UserId, req.PlanId); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<CancelResultDto>> Cancel(int id)
    {
        var (ok, result, error) = await subs.CancelAsync(UserId, id);
        return ok ? Ok(result) : Conflict(new { error });
    }
}

/// <summary>Receives PayFast's server-to-server notification (ITN). The app is never trusted to say a payment succeeded.</summary>
[ApiController]
public class PayFastController(
    SubscriptionService subs, PayFastService payFast, IHttpClientFactory http,
    Microsoft.Extensions.Configuration.IConfiguration cfg, ILogger<PayFastController> log) : ControllerBase
{
    [AllowAnonymous, HttpPost("api/payfast/itn")]
    public async Task<IActionResult> Itn(CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);
        var posted = form.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value.ToString())).ToList();
        string Get(string k) => posted.FirstOrDefault(p => p.Key == k).Value ?? "";

        // 1. Signature
        if (!payFast.VerifySignature(posted, Get("signature"))) { log.LogWarning("ITN rejected: bad signature"); return BadRequest(); }
        // 2. Merchant
        if (Get("merchant_id") != payFast.MerchantId) { log.LogWarning("ITN rejected: wrong merchant"); return BadRequest(); }
        // 3. Confirm with PayFast that this notification is genuine (skippable only in local testing)
        if (cfg.GetValue("PayFast:ValidateWithServer", true))
        {
            var body = payFast.ToQueryString(posted.Where(p => p.Key != "signature"));
            var resp = await http.CreateClient().PostAsync(payFast.ValidateUrl,
                new StringContent(body, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"), ct);
            if ((await resp.Content.ReadAsStringAsync(ct)).Trim() != "VALID") { log.LogWarning("ITN rejected: PayFast did not validate"); return BadRequest(); }
        }
        // 4. Amount is checked against the plan price inside ApplyPaymentResultAsync
        if (!int.TryParse(Get("m_payment_id"), out var subId) ||
            !decimal.TryParse(Get("amount_gross"), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            return BadRequest();

        var ok = await subs.ApplyPaymentResultAsync(subId, Get("payment_status"), amount, Get("pf_payment_id"), Get("token"));
        return ok ? Ok() : BadRequest();
    }

    // Simple landing pages for the browser after checkout.
    [AllowAnonymous, HttpGet("payfast/return")]
    public ContentResult Return() => Page("Payment received", "Thank you! You can close this page and go back to the Rhythm & Flow app. Your subscription will activate as soon as PayFast confirms the payment.");

    [AllowAnonymous, HttpGet("payfast/cancel")]
    public ContentResult Cancel() => Page("Payment cancelled", "No payment was taken. You can close this page and go back to the app.");

    private static ContentResult Page(string title, string text) => new()
    {
        ContentType = "text/html; charset=utf-8",
        Content = $"<!doctype html><meta name=viewport content='width=device-width,initial-scale=1'><title>{title}</title>" +
                  "<body style='font-family:Georgia,serif;background:#fff;color:#000;text-align:center;padding:48px 24px'>" +
                  $"<h1 style='color:#279989'>{title}</h1><p style='font-family:system-ui;line-height:1.5'>{text}</p></body>"
    };
}

/// <summary>Development-only helpers. PayFast cannot reach localhost, so this simulates the ITN a real payment would trigger.</summary>
[ApiController, Authorize]
[Route("api/dev")]
public class DevController(SubscriptionService subs, Microsoft.Extensions.Hosting.IHostEnvironment env, Data.AppDbContext db) : ApiController
{
    /// <summary>Throws on purpose so the error logging and admin alerts can be demonstrated and tested.</summary>
    [HttpGet("boom")]
    public IActionResult Boom()
    {
        if (!env.IsDevelopment()) return NotFound();
        throw new InvalidOperationException("Test error triggered from /api/dev/boom");
    }

    [HttpPost("simulate-payment/{subscriptionId:int}")]
    public async Task<IActionResult> SimulatePayment(int subscriptionId)
    {
        if (!env.IsDevelopment()) return NotFound();
        var sub = await db.Subscriptions.Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == subscriptionId && s.UserId == UserId);
        if (sub is null) return NotFound();
        var ok = await subs.ApplyPaymentResultAsync(sub.Id, "COMPLETE", sub.Plan!.Price, "SIM-" + Guid.NewGuid().ToString("N")[..12], "SIM-TOKEN");
        return ok ? NoContent() : BadRequest();
    }
}
