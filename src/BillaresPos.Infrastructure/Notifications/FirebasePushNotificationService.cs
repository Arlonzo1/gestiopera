using BillaresPos.Application.Common.Abstractions;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BillaresPos.Infrastructure.Notifications;

public sealed class FirebasePushNotificationService : IPushNotificationService
{
    private readonly FirebaseMessaging messaging;
    private readonly ILogger<FirebasePushNotificationService> logger;

    public FirebasePushNotificationService(
        IConfiguration configuration,
        ILogger<FirebasePushNotificationService> logger)
    {
        this.logger = logger;
        var projectId = configuration["Firebase:ProjectId"];
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new InvalidOperationException("Firebase:ProjectId is required when Firebase:Enabled is true.");
        }

        var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.GetApplicationDefault(),
            ProjectId = projectId
        });
        messaging = FirebaseMessaging.GetMessaging(app);
    }

    public async Task SendAsync(
        IReadOnlyCollection<string> deviceTokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default)
    {
        foreach (var batch in deviceTokens.Chunk(500))
        {
            var messages = batch.Select(token => new Message
            {
                #pragma warning disable CS0618 // FCM web registration tokens use the Token field; FID is a different identifier.
                Token = token,
                #pragma warning restore CS0618
                Notification = new FirebaseAdmin.Messaging.Notification { Title = title, Body = body },
                Data = new Dictionary<string, string>(data)
            });
            var response = await messaging.SendEachAsync(messages, cancellationToken);
            var failures = response.Responses
                .Where(x => !x.IsSuccess)
                .Select(x => x.Exception?.Message ?? "Firebase rejected a push message.")
                .ToArray();
            if (failures.Length > 0)
            {
                logger.LogWarning("Firebase rejected {FailureCount} push message(s).", failures.Length);
                throw new InvalidOperationException(string.Join(" | ", failures));
            }
        }
    }
}
