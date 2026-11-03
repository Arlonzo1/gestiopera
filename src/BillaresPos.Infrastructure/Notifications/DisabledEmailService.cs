using BillaresPos.Application.Common.Abstractions;

namespace BillaresPos.Infrastructure.Notifications;

public sealed class DisabledEmailService : IEmailService
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
}