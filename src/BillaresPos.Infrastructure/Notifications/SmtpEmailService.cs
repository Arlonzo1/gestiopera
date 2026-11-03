using System.Net;
using System.Net.Mail;
using BillaresPos.Application.Common.Abstractions;
using Microsoft.Extensions.Configuration;

namespace BillaresPos.Infrastructure.Notifications;

public sealed class SmtpEmailService(IConfiguration configuration) : IEmailService
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var host = configuration["Email:SmtpHost"];
        var from = configuration["Email:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            throw new InvalidOperationException("Email is enabled but SMTP host or sender is missing.");

        var port = int.TryParse(configuration["Email:SmtpPort"], out var configuredPort) ? configuredPort : 587;
        var enableSsl = !bool.TryParse(configuration["Email:EnableSsl"], out var ssl) || ssl;
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(configuration["Email:SmtpUser"], configuration["Email:SmtpPassword"])
        };
        using var mail = new MailMessage(from, message.Recipient, message.Subject, message.Body)
        {
            IsBodyHtml = false
        };
        if (message.AttachmentContent is not null && !string.IsNullOrWhiteSpace(message.AttachmentFileName))
            mail.Attachments.Add(new Attachment(new MemoryStream(message.AttachmentContent), message.AttachmentFileName, "application/pdf"));
        await client.SendMailAsync(mail, cancellationToken);
    }
}