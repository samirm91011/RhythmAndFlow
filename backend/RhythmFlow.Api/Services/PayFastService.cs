using System.Security.Cryptography;
using System.Text;
using RhythmFlow.Api.Domain;

namespace RhythmFlow.Api.Services;

public class PayFastOptions
{
    public bool Sandbox { get; set; } = true;
    public string MerchantId { get; set; } = "";
    public string MerchantKey { get; set; } = "";
    public string Passphrase { get; set; } = "";
    /// <summary>Public base URL of this API (PayFast must be able to reach it for ITN). Use a tunnel such as ngrok when testing locally.</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5080";
    /// <summary>Where the browser is sent after payment (a page on this API that explains how to return to the app).</summary>
    public string ReturnPath { get; set; } = "/payfast/return";
    public string CancelPath { get; set; } = "/payfast/cancel";
}

/// <summary>Builds PayFast payment links and verifies ITN notifications. Cards are never handled by this app.</summary>
public class PayFastService(Microsoft.Extensions.Options.IOptions<PayFastOptions> options)
{
    private readonly PayFastOptions _o = options.Value;

    public string ProcessHost => _o.Sandbox ? "https://sandbox.payfast.co.za" : "https://www.payfast.co.za";
    public string MerchantId => _o.MerchantId;

    /// <summary>Recurring (monthly) payment link. Field order matters for the signature.</summary>
    public string BuildPaymentUrl(User user, SubscriptionPlan plan, Subscription sub)
    {
        var first = user.FullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var amount = plan.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        var fields = new List<KeyValuePair<string, string>>
        {
            new("merchant_id", _o.MerchantId),
            new("merchant_key", _o.MerchantKey),
            new("return_url", _o.PublicBaseUrl.TrimEnd('/') + _o.ReturnPath),
            new("cancel_url", _o.PublicBaseUrl.TrimEnd('/') + _o.CancelPath),
            new("notify_url", _o.PublicBaseUrl.TrimEnd('/') + "/api/payfast/itn"),
            new("name_first", first.ElementAtOrDefault(0) ?? ""),
            new("name_last", first.ElementAtOrDefault(1) ?? ""),
            new("email_address", user.Email),
            new("m_payment_id", sub.Id.ToString()),
            new("amount", amount),
            new("item_name", $"Rhythm & Flow - {plan.Name}"),
            new("item_description", "Monthly subscription"),
            new("subscription_type", "1"),
            new("billing_date", DateTime.UtcNow.ToString("yyyy-MM-dd")),
            new("recurring_amount", amount),
            new("frequency", "3"),   // 3 = monthly
            new("cycles", "0"),      // 0 = indefinite
        };
        var query = ToQueryString(fields.Where(f => !string.IsNullOrEmpty(f.Value)));
        // PayFast's shared public test merchant (10000100) accepts unsigned requests but rejects any signature, because its
        // passphrase is not ours to know. So in sandbox mode with no passphrase configured we send the request unsigned.
        // A real or personal sandbox merchant must set a passphrase, which switches signing on (always required in production).
        if (_o.Sandbox && string.IsNullOrEmpty(_o.Passphrase))
            return $"{ProcessHost}/eng/process?{query}";
        var sig = Md5(AppendPassphrase(query));
        return $"{ProcessHost}/eng/process?{query}&signature={sig}";
    }

    /// <summary>Verifies the signature of an ITN: all posted fields except "signature", in posted order.</summary>
    public bool VerifySignature(IEnumerable<KeyValuePair<string, string>> posted, string signature)
    {
        var query = ToQueryString(posted.Where(p => p.Key != "signature"));
        return string.Equals(Md5(AppendPassphrase(query)), signature, StringComparison.OrdinalIgnoreCase);
    }

    public string ValidateUrl => $"{ProcessHost}/eng/query/validate";

    public string ToQueryString(IEnumerable<KeyValuePair<string, string>> fields) =>
        string.Join("&", fields.Select(f => $"{f.Key}={Encode(f.Value.Trim())}"));

    private string AppendPassphrase(string query) =>
        string.IsNullOrEmpty(_o.Passphrase) ? query : $"{query}&passphrase={Encode(_o.Passphrase.Trim())}";

    private static string Md5(string s) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    /// <summary>Matches PHP's urlencode(), which PayFast uses when computing signatures.</summary>
    public static string UrlEncode(string value) => Encode(value);

    private static string Encode(string value)
    {
        var sb = new StringBuilder();
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            var c = (char)b;
            if (char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.') sb.Append(c);
            else if (c == ' ') sb.Append('+');
            else sb.Append('%').Append(b.ToString("X2"));
        }
        return sb.ToString();
    }
}
