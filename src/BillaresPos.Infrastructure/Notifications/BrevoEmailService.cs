using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BillaresPos.Application.Common.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BillaresPos.Infrastructure.Notifications;

public sealed class BrevoEmailService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<BrevoEmailService> logger) : IEmailService
{
    private const string Endpoint = "https://api.brevo.com/v3/smtp/email";

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["Email:BrevoApiKey"];
        var from = configuration["Email:From"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("El correo con Brevo está activado, pero falta la API key o el remitente.");

        var request = new BrevoEmailRequest
        {
            Sender = new BrevoContact
            {
                Email = from,
                Name = configuration["Email:FromName"] ?? "GestiOpera"
            },
            To = [new BrevoContact { Email = message.Recipient }],
            Subject = message.Subject,
            TextContent = message.Body,
            Attachment = message.AttachmentContent is not null && !string.IsNullOrWhiteSpace(message.AttachmentFileName)
                ? [new BrevoAttachment
                {
                    Content = Convert.ToBase64String(message.AttachmentContent),
                    Name = message.AttachmentFileName
                }]
                : null
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        httpRequest.Headers.Add("api-key", apiKey);
        httpRequest.Content = JsonContent.Create(request);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        if (response.IsSuccessStatusCode)
            return;

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogError("Brevo rejected an email with status {StatusCode}: {Detail}", response.StatusCode, detail);
        throw new InvalidOperationException($"Brevo rejected the email ({(int)response.StatusCode}).");
    }

    private sealed class BrevoEmailRequest
    {
        [JsonPropertyName("sender")] public required BrevoContact Sender { get; init; }
        [JsonPropertyName("to")] public required IReadOnlyList<BrevoContact> To { get; init; }
        [JsonPropertyName("subject")] public required string Subject { get; init; }
        [JsonPropertyName("textContent")] public required string TextContent { get; init; }
        [JsonPropertyName("attachment")] public IReadOnlyList<BrevoAttachment>? Attachment { get; init; }
    }

    private sealed class BrevoContact
    {
        [JsonPropertyName("email")] public required string Email { get; init; }
        [JsonPropertyName("name")] public string? Name { get; init; }
    }

    private sealed class BrevoAttachment
    {
        [JsonPropertyName("content")] public required string Content { get; init; }
        [JsonPropertyName("name")] public required string Name { get; init; }
    }
}
