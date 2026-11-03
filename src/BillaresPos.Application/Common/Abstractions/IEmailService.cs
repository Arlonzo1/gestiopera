namespace BillaresPos.Application.Common.Abstractions;

public sealed record EmailMessage(string Recipient, string Subject, string Body, byte[]? AttachmentContent = null, string? AttachmentFileName = null);

public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}