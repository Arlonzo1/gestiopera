namespace BillaresPos.Application.Common.Abstractions;

public interface IPushNotificationService
{
    Task SendAsync(
        IReadOnlyCollection<string> deviceTokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default);
}
