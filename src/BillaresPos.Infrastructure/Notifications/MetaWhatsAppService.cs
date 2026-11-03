using System.Net.Http.Headers;
using System.Net.Http.Json;
using BillaresPos.Application.Common.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BillaresPos.Infrastructure.Notifications;

public sealed class MetaWhatsAppService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<MetaWhatsAppService> logger) : IWhatsAppService
{
    public async Task SendAsync(WhatsAppMessage message, CancellationToken cancellationToken = default)
    {
        var accessToken = configuration["WhatsApp:AccessToken"];
        var phoneNumberId = configuration["WhatsApp:PhoneNumberId"];
        var apiVersion = configuration["WhatsApp:ApiVersion"] ?? "v25.0";

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(phoneNumberId))
            throw new InvalidOperationException("WhatsApp Cloud API is enabled but AccessToken or PhoneNumberId is missing.");

        var endpoint = $"https://graph.facebook.com/{apiVersion}/{phoneNumberId}/messages";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(BuildPayload(message));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Meta WhatsApp API returned {StatusCode}: {ResponseBody}", response.StatusCode, responseBody);
            throw new HttpRequestException($"Meta WhatsApp API returned {(int)response.StatusCode}.");
        }

        logger.LogInformation("WhatsApp message sent to {RecipientPhoneNumber}.", message.RecipientPhoneNumber);
    }

    public async Task SendDocumentAsync(string recipientPhoneNumber, byte[] content, string fileName, string caption, CancellationToken cancellationToken = default)
    {
        var accessToken = configuration["WhatsApp:AccessToken"];
        var phoneNumberId = configuration["WhatsApp:PhoneNumberId"];
        var apiVersion = configuration["WhatsApp:ApiVersion"] ?? "v25.0";
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(phoneNumberId))
            throw new InvalidOperationException("WhatsApp Cloud API is enabled but AccessToken or PhoneNumberId is missing.");
        using var media = new MultipartFormDataContent();
        media.Add(new StringContent("whatsapp"), "messaging_product");
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        media.Add(fileContent, "file", fileName);
        using var upload = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/{apiVersion}/{phoneNumberId}/media") { Content = media };
        upload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var uploadResponse = await httpClient.SendAsync(upload, cancellationToken);
        var uploadBody = await uploadResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!uploadResponse.IsSuccessStatusCode)
            throw new HttpRequestException($"Meta WhatsApp media upload returned {(int)uploadResponse.StatusCode}: {uploadBody}");
        using var uploadJson = System.Text.Json.JsonDocument.Parse(uploadBody);
        var mediaId = uploadJson.RootElement.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(mediaId)) throw new InvalidOperationException("Meta did not return a media id.");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/{apiVersion}/{phoneNumberId}/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new { messaging_product = "whatsapp", recipient_type = "individual", to = recipientPhoneNumber, type = "document", document = new { id = mediaId, caption, filename = fileName } });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Meta WhatsApp document returned {(int)response.StatusCode}: {responseBody}");
        logger.LogInformation("WhatsApp document sent to {RecipientPhoneNumber}.", recipientPhoneNumber);
    }
    private static object BuildPayload(WhatsAppMessage message) => message.Type switch
    {
        WhatsAppMessageType.Text => new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = message.RecipientPhoneNumber,
            type = "text",
            text = new { body = message.TextBody ?? string.Empty }
        },
        _ => new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = message.RecipientPhoneNumber,
            type = "template",
            template = new
            {
                name = message.TemplateName ?? throw new InvalidOperationException("WhatsApp template name is required."),
                language = new { code = message.LanguageCode ?? "es" },
                components = message.BodyParameters is not { Count: > 0 }
                    ? null
                    : new[]
                    {
                        new
                        {
                            type = "body",
                            parameters = message.BodyParameters.Select(text => new { type = "text", text }).ToArray()
                        }
                    }
            }
        }
    };
}