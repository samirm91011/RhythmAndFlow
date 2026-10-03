using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace RhythmFlow.Api.Services;

/// <summary>Calls PayFast's merchant API (not the checkout page). Needs the merchant ID and the account passphrase.</summary>
public interface IPayFastApi
{
    bool CanCall { get; }
    Task<(bool Ok, string Message)> CancelSubscriptionAsync(string token);
}

public class PayFastApiClient(IHttpClientFactory http, IOptions<PayFastOptions> options, ILogger<PayFastApiClient> log) : IPayFastApi
{
    private readonly PayFastOptions _o = options.Value;

    public bool CanCall => !string.IsNullOrWhiteSpace(_o.MerchantId) && !string.IsNullOrWhiteSpace(_o.Passphrase);

    /// <summary>Stops future recurring charges for a subscription token. PayFast keeps no card details in our system.</summary>
    public async Task<(bool Ok, string Message)> CancelSubscriptionAsync(string token)
    {
        if (!CanCall) return (false, "PayFast API access is not configured (merchant ID and passphrase are required).");
        try
        {
            var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
            // Signature: header values plus the passphrase, sorted by name, URL-encoded, joined with &, then MD5.
            var parts = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["merchant-id"] = _o.MerchantId, ["passphrase"] = _o.Passphrase, ["timestamp"] = timestamp, ["version"] = "v1",
            };
            var signature = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(
                string.Join("&", parts.Select(p => $"{p.Key}={PayFastService.UrlEncode(p.Value)}"))))).ToLowerInvariant();

            var url = $"https://api.payfast.co.za/subscriptions/{Uri.EscapeDataString(token)}/cancel" + (_o.Sandbox ? "?testing=true" : "");
            using var req = new HttpRequestMessage(HttpMethod.Put, url);
            req.Headers.Add("merchant-id", _o.MerchantId);
            req.Headers.Add("version", "v1");
            req.Headers.Add("timestamp", timestamp);
            req.Headers.Add("signature", signature);

            using var resp = await http.CreateClient().SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode) return (true, "Cancelled with PayFast.");
            // PayFast answers 400 if the subscription is already cancelled; treat that as success so the app can catch up.
            if (body.Contains("already", StringComparison.OrdinalIgnoreCase)) return (true, "Already cancelled with PayFast.");
            log.LogWarning("PayFast cancel failed: {Status} {Body}", (int)resp.StatusCode, body);
            return (false, $"PayFast could not cancel this subscription (error {(int)resp.StatusCode}).");
        }
        catch (Exception ex)
        {
            log.LogError(ex, "PayFast cancel call failed");
            return (false, "We couldn't reach PayFast to cancel. Please try again in a moment.");
        }
    }
}
