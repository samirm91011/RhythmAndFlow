using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace RhythmFlow.Api.Services;

public class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Rhythm & Flow";
    public bool EnableSsl { get; set; } = true;
}

public class AdminOptions
{
    /// <summary>Addresses that receive an email when a new error is reported.</summary>
    public string[] AlertEmails { get; set; } = [];
}

/// <summary>
/// Sends email through SMTP. When SMTP is not configured the message is written to the log instead, so reset codes are
/// still usable during development.
/// </summary>
public interface IEmailSender
{
    Task<bool> SendAsync(string to, string subject, string body);
}

public class EmailService(IOptions<SmtpOptions> options, ILogger<EmailService> log) : IEmailSender
{
    private readonly SmtpOptions _o = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_o.Host) && !string.IsNullOrWhiteSpace(_o.FromAddress);

    public async Task<bool> SendAsync(string to, string subject, string body)
    {
        if (!IsConfigured)
        {
            log.LogWarning("EMAIL NOT SENT (SMTP is not configured). To: {To} | Subject: {Subject} | Body: {Body}", to, subject, body);
            return false;
        }
        try
        {
            using var client = new SmtpClient(_o.Host, _o.Port)
            {
                EnableSsl = _o.EnableSsl,
                Credentials = string.IsNullOrEmpty(_o.User) ? null : new NetworkCredential(_o.User, _o.Password),
                Timeout = 15000,
            };
            using var msg = new MailMessage { From = new MailAddress(_o.FromAddress, _o.FromName), Subject = subject, Body = body, IsBodyHtml = false };
            msg.To.Add(to);
            await client.SendMailAsync(msg);
            return true;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Could not send email to {To}", to);
            return false;
        }
    }
}
