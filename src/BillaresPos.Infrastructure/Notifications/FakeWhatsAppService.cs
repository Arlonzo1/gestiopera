using BillaresPos.Application.Common.Abstractions;
using Microsoft.Extensions.Logging;

namespace BillaresPos.Infrastructure.Notifications;

public sealed class FakeWhatsAppService(ILogger<FakeWhatsAppService> logger) : IWhatsAppService
{
    public Task SendAsync(WhatsAppMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Fake WhatsApp message queued for {RecipientPhoneNumber}. Type: {MessageType}, Template: {TemplateName}",
            message.RecipientPhoneNumber, message.Type, message.TemplateName);
        return Task.CompletedTask;
    }
    public Task SendDocumentAsync(string recipientPhoneNumber, byte[] content, string fileName, string caption, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Fake WhatsApp document queued for {RecipientPhoneNumber}: {FileName}", recipientPhoneNumber, fileName);
        return Task.CompletedTask;
    }}