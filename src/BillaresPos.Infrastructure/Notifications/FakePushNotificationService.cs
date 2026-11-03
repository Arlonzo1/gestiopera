using BillaresPos.Application.Common.Abstractions;
using Microsoft.Extensions.Logging;

namespace BillaresPos.Infrastructure.Notifications;

public sealed class FakePushNotificationService(ILogger<FakePushNotificationService> logger) : IPushNotificationService
{
    public Task SendAsync(
        IReadOnlyCollection<string> deviceTokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Fake push notification sent to {DeviceCount} device(s): {Title}. Data keys: {DataKeys}",
            deviceTokens.Count,
            title,
            string.Join(",", data.Keys));
        return Task.CompletedTask;
    }
}
