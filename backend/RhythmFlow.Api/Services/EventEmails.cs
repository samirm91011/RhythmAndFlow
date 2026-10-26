using System.Globalization;
using System.Text;
using RhythmFlow.Api.Domain;

namespace RhythmFlow.Api.Services;

public class EventOptions
{
    public string ContactEmail { get; set; } = "info@rhythmandflow.co.za";
    public string ContactWhatsApp { get; set; } = "+27-83-440-3317";
    /// <summary>A customer who cancels at least this many hours before the event can choose a refund or credit for the full fee.</summary>
    public int FullRefundHours { get; set; } = 48;
    /// <summary>How long a ticket is held while the customer pays on PayFast.</summary>
    public int HoldMinutes { get; set; } = 30;
    /// <summary>How long a spot that opens on a waiting list is held for the first person on it.</summary>
    public int WaitlistOfferHours { get; set; } = 12;
    public int CreditMonths { get; set; } = 12;
    /// <summary>The most that can be agreed for a cancellation asked for too late, as a share of the fee.</summary>
    public int LateCancelMaxPercent { get; set; } = 50;
}

/// <summary>The wording of the booking emails, built from what the client asked for. Times are shown in South African time.</summary>
public static class EventEmails
{
    public const string SignOff = "Thank you for making space for yourself with Rhythm & Flow. We look forward to moving and flowing with you.";

    /// <summary>South African time is UTC+2 all year (no daylight saving).</summary>
    public static DateTime Sast(DateTime utc) => utc.AddHours(2);

    public static string When(ClassSession c)
    {
        var s = Sast(c.StartTime); var minutes = (int)(c.EndTime - c.StartTime).TotalMinutes;
        return $"{s.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture)}, {s:HH:mm} (South African time), {Duration(minutes)}";
    }

    public static string Duration(int minutes) =>
        minutes >= 60 && minutes % 60 == 0 ? $"{minutes / 60} {(minutes == 60 ? "hour" : "hours")}"
        : minutes > 60 ? $"{minutes / 60} h {minutes % 60} min" : $"{minutes} minutes";

    public static string Rand(decimal amount) => "R" + amount.ToString("0.00", CultureInfo.InvariantCulture);

    public static string Reference(int bookingId) => $"RF-E{bookingId:D6}";

    private static string KindLabel(ClassSession c) => c.Kind switch
    {
        EventKinds.Workshop => "workshop", EventKinds.Retreat => "retreat", EventKinds.Coaching => "coaching session", _ => "class",
    };

    /// <summary>The "what to bring" paragraph for this kind of event, in the client's words. An event can replace it with its own note.</summary>
    public static string PracticalParagraph(ClassSession c)
    {
        if (!string.IsNullOrWhiteSpace(c.PracticalNote)) return c.PracticalNote.Trim();
        if (c.Format == EventKinds.Online && c.Kind != EventKinds.Coaching)
            return "For online sessions: Please join using the link provided, ideally 5–10 minutes before the scheduled start time. Find a quiet, comfortable space where you can participate without interruption, and have any recommended materials nearby. If you are moving, allow enough space to move safely.";
        return c.Kind switch
        {
            EventKinds.Coaching => "For coaching sessions: Please arrive or log in a few minutes early, find a quiet and private space, and bring any notes or reflections you would like to discuss. There is no need to prepare anything else unless specified.",
            EventKinds.Workshop => "For wellness workshops: Please check the event details for any recommended materials or preparation. Arrive a few minutes early so you can settle in and make the most of the experience.",
            EventKinds.Retreat => "For retreats or extended experiences: Please refer to the event-specific information for arrival times, venue details, what to bring, meals, accommodation (if applicable), and any preparation required. We will communicate any additional arrangements before the event.",
            _ => "For movement classes: Please arrive 5–10 minutes before the session begins, wearing comfortable clothing suitable for movement. Bring a water bottle and any equipment specified in the event details.",
        };
    }

    public static string FormatLine(ClassSession c) =>
        c.Format == EventKinds.Online
            ? string.IsNullOrWhiteSpace(c.JoinLink)
                ? $"Online. A separate email with the {(string.IsNullOrWhiteSpace(c.JoinPlatform) ? "platform" : c.JoinPlatform)} joining link will follow."
                : $"Online on {(string.IsNullOrWhiteSpace(c.JoinPlatform) ? "the platform" : c.JoinPlatform)}. The joining link is in a separate email."
            : $"In person at {c.Location}";

    /// <summary>The booking confirmation. Everything the client listed: event, time, format and venue, price, reference, attendee, contact details, practical note, policy, sign-off.</summary>
    public static (string Subject, string Body) Confirmation(User user, ClassSession c, Booking b, EventOptions o)
    {
        var paid = b.AmountPaid + b.CreditApplied;
        var sb = new StringBuilder();
        sb.AppendLine($"Hi {user.FullName.Split(' ')[0]},");
        sb.AppendLine();
        sb.AppendLine($"Your booking is confirmed. Here are your details.");
        sb.AppendLine();
        sb.AppendLine($"Event: {c.Name}");
        sb.AppendLine($"Date and time: {When(c)}");
        sb.AppendLine($"Format: {FormatLine(c)}");
        sb.AppendLine($"Price paid: {Rand(paid)} (ZAR){(b.CreditApplied > 0 ? $", including {Rand(b.CreditApplied)} account credit" : "")}");
        sb.AppendLine($"Booking reference: {Reference(b.Id)}");
        sb.AppendLine($"Attendee: {user.FullName} ({user.Email})");
        sb.AppendLine();
        sb.AppendLine("Rhythm & Flow contact details");
        sb.AppendLine($"Email: {o.ContactEmail}");
        sb.AppendLine($"WhatsApp: {o.ContactWhatsApp}");
        sb.AppendLine();
        sb.AppendLine("Practical Information for Your Session");
        sb.AppendLine();
        sb.AppendLine("To help you feel comfortable and prepared, please review the practical details for your upcoming Rhythm & Flow experience below.");
        sb.AppendLine();
        sb.AppendLine(PracticalParagraph(c));
        sb.AppendLine();
        sb.AppendLine("If you have any questions or need assistance before your session, please contact us using the details provided in this confirmation.");
        sb.AppendLine();
        sb.AppendLine("We look forward to welcoming you and sharing this time with you.");
        sb.AppendLine();
        sb.AppendLine("Cancellation, refund and rescheduling policy");
        sb.AppendLine();
        sb.AppendLine(PolicyContent.Find(PolicyContent.EventCancellation)!.Body);
        sb.AppendLine();
        sb.AppendLine(SignOff);
        return ($"Your booking is confirmed: {c.Name} ({Reference(b.Id)})", sb.ToString());
    }

    /// <summary>The separate email with the Zoom or Google Meet link.</summary>
    public static (string Subject, string Body) JoinLink(User user, ClassSession c, Booking b)
    {
        var platform = string.IsNullOrWhiteSpace(c.JoinPlatform) ? "Online" : c.JoinPlatform;
        var sb = new StringBuilder();
        sb.AppendLine($"Hi {user.FullName.Split(' ')[0]},");
        sb.AppendLine();
        sb.AppendLine($"Here is how to join {c.Name}.");
        sb.AppendLine();
        sb.AppendLine($"Date and time: {When(c)}");
        sb.AppendLine($"Platform: {platform}");
        sb.AppendLine($"Joining link: {c.JoinLink}");
        sb.AppendLine($"Booking reference: {Reference(b.Id)}");
        sb.AppendLine();
        sb.AppendLine("Please join 5–10 minutes before the scheduled start time. Find a quiet, comfortable space where you can participate without interruption.");
        sb.AppendLine();
        sb.AppendLine(SignOff);
        return ($"Joining details for {c.Name}", sb.ToString());
    }

    public static (string Subject, string Body) Reminder(User user, ClassSession c, Booking b, bool oneHour)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Hi {user.FullName.Split(' ')[0]},");
        sb.AppendLine();
        sb.AppendLine(oneHour ? $"{c.Name} starts in about an hour." : $"A reminder that {c.Name} is coming up within 24 hours.");
        sb.AppendLine();
        sb.AppendLine($"Date and time: {When(c)}");
        sb.AppendLine($"Format: {FormatLine(c)}");
        if (oneHour && c.Format == EventKinds.Online && !string.IsNullOrWhiteSpace(c.JoinLink)) sb.AppendLine($"Joining link: {c.JoinLink}");
        sb.AppendLine($"Booking reference: {Reference(b.Id)}");
        sb.AppendLine();
        sb.AppendLine(SignOff);
        return (oneHour ? $"Starting soon: {c.Name}" : $"Tomorrow: {c.Name}", sb.ToString());
    }

    public static string Describe(ClassSession c) => $"{c.Name} on {Sast(c.StartTime):ddd d MMM} at {Sast(c.StartTime):HH:mm}";

    public static string KindWord(ClassSession c) => KindLabel(c);
}
