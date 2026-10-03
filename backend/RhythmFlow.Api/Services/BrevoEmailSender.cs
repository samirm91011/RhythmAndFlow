using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace RhythmFlow.Api.Services;

public class BrevoOptions
{
    /// <summary>API key from Brevo (SMTP &amp; API → API keys). Sends over HTTPS, so it works on hosts that block SMTP ports.</summary>
    public string ApiKey { get; set; } = "";
    /// <summary>A sender address verified in Brevo (Senders &amp; IP → Senders). A Gmail address works for testing.</summary>
    public string SenderEmail { get; set; } = "";
    public string SenderName { get; set; } = "Rhythm & Flow";
}

/// <summary>Sends email through Brevo's web API. Selected with Email:Provider = Brevo. Falls back to logging when not configured.</summary>
public class BrevoEmailSender(IHttpClientFactory http, IOptions<BrevoOptions> options, ILogger<BrevoEmailSender> log) : IEmailSender
{
    private readonly BrevoOptions _o = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_o.ApiKey) && !string.IsNullOrWhiteSpace(_o.SenderEmail);

    public async Task<bool> SendAsync(string to, string subject, string body)
    {
        if (!IsConfigured)
        {
            log.LogWarning("EMAIL NOT SENT (Brevo is not configured). To: {To} | Subject: {Subject} | Body: {Body}", to, subject, body);
            return false;
        }
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email")
            {
                Content = JsonContent.Create(new
                {
                    sender = new { name = _o.SenderName, email = _o.SenderEmail },
                    to = new[] { new { email = to } },
                    subject,
                    textContent = body,
                }),
            };
            req.Headers.Add("api-key", _o.ApiKey);
            req.Headers.Add("accept", "application/json");
            using var resp = await http.CreateClient().SendAsync(req);
            if (resp.IsSuccessStatusCode) return true;
            log.LogError("Brevo rejected the email to {To}: {Status} {Body}", to, (int)resp.StatusCode, await resp.Content.ReadAsStringAsync());
            return false;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Could not send email to {To} through Brevo", to);
            return false;
        }
    }
}
