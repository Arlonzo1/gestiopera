namespace BillaresPos.Application.Common.Abstractions;

public enum WhatsAppMessageType
{
    Template,
    Text
}

public sealed record WhatsAppMessage(
    WhatsAppMessageType Type,
    string RecipientPhoneNumber,
    string? TemplateName = null,
    string? LanguageCode = null,
    IReadOnlyList<string>? BodyParameters = null,
    string? TextBody = null);

public interface IWhatsAppService
{
    Task SendAsync(WhatsAppMessage message, CancellationToken cancellationToken = default);
    Task SendDocumentAsync(string recipientPhoneNumber, byte[] content, string fileName, string caption, CancellationToken cancellationToken = default);
}