using FirebaseAdmin.Messaging;

namespace RhythmFlow.Api.Services;

/// <summary>What the phone needs to show a notification and open the right screen when it is tapped.</summary>
public record PushMessage(string Title, string Body, string Kind, string? Route, int NotificationId);

public interface IPushSender
{
    /// <summary>False when no Firebase key is configured; the app then relies on its periodic sync instead.</summary>
    bool Enabled { get; }

    /// <summary>Sends to the given phones and returns the tokens Firebase says are no longer valid, so they can be forgotten.</summary>
    Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken ct = default);
}

public class NullPushSender : IPushSender
{
    public bool Enabled => false;
    public Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);
}

/// <summary>
/// Sends through Firebase Cloud Messaging. Messages carry data only (no "notification" block) so the app builds the
/// notification itself: same look, same channel, and the same id as the in-app copy, so nothing shows twice.
/// </summary>
public class FirebasePushSender(FirebaseMessaging messaging, ILogger<FirebasePushSender> log) : IPushSender
{
    public bool Enabled => true;

    public async Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<string> tokens, PushMessage message, CancellationToken ct = default)
    {
        var invalid = new List<string>();
        if (tokens.Count == 0) return invalid;

        var data = new Dictionary<string, string>
        {
            ["id"] = message.NotificationId.ToString(),
            ["title"] = message.Title,
            ["body"] = message.Body,
            ["kind"] = message.Kind,
            ["route"] = message.Route ?? "",
        };

        foreach (var batch in tokens.Chunk(500))
        {
            // "Tokens" is marked deprecated in favour of Fids, but these are FCM registration tokens (what the app sends us),
            // which is exactly what Tokens carries.
#pragma warning disable CS0618
            var response = await messaging.SendEachForMulticastAsync(new MulticastMessage
            {
                Tokens = batch.ToList(),
                Data = data,
                Android = new AndroidConfig { Priority = Priority.High, TimeToLive = TimeSpan.FromHours(6) },
            }, ct);
#pragma warning restore CS0618

            for (var i = 0; i < response.Responses.Count; i++)
            {
                var r = response.Responses[i];
                if (r.IsSuccess) continue;
                var code = r.Exception?.MessagingErrorCode;
                if (code is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument) invalid.Add(batch[i]);
                else log.LogWarning("Push to one phone failed: {Code} {Message}", code, r.Exception?.Message);
            }
        }
        return invalid;
    }
}
