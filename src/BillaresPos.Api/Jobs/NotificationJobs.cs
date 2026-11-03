using BillaresPos.Application.Common.Abstractions;
using BillaresPos.Domain.Notifications;
using BillaresPos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BillaresPos.Api.Jobs;

public sealed class NotificationJobs(IServiceScopeFactory scopeFactory, IConfiguration configuration)
{
    public async Task DeliverAsync(Guid notificationId)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var whatsAppService = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var notification = await dbContext.Notifications.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == notificationId);
        if (notification is null || notification.Status == NotificationStatus.Sent) return;
        try
        {
            if (notification.Channel == NotificationChannel.Push)
            {
                var pushService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
                var tokens = await dbContext.PushDeviceTokens.IgnoreQueryFilters()
                    .Where(x => x.TenantId == notification.TenantId && x.UserId == notification.RecipientUserId && x.IsActive)
                    .Select(x => x.Token).ToListAsync();
                if (tokens.Count > 0)
                    await pushService.SendAsync(tokens, notification.Title, notification.Body, new Dictionary<string, string>
                    {
                        ["referenceType"] = notification.ReferenceType ?? string.Empty,
                        ["referenceId"] = notification.ReferenceId?.ToString() ?? string.Empty
                    });
            }
            else if (notification.Channel == NotificationChannel.Email)
            {
                var recipient = await dbContext.NotificationPreferences.IgnoreQueryFilters()
                    .Where(x => x.UserId == notification.RecipientUserId && x.TenantId == notification.TenantId)
                    .Select(x => x.EmailAddress).SingleOrDefaultAsync()
                    ?? await dbContext.Users.IgnoreQueryFilters()
                        .Where(x => x.Id == notification.RecipientUserId && x.TenantId == notification.TenantId)
                        .Select(x => x.Email).SingleOrDefaultAsync();
                if (string.IsNullOrWhiteSpace(recipient)) throw new InvalidOperationException("El propietario no tiene un correo configurado.");
                byte[]? attachment = null;
                string? fileName = null;
                var sendReceipt = bool.TryParse(configuration["Email:SendReceipt"], out var receiptEnabled) && receiptEnabled && notification.ReferenceType == "Sale";
                if (sendReceipt && notification.ReferenceId is Guid saleId)
                {
                    var document = await dbContext.GeneratedDocuments.IgnoreQueryFilters()
                        .SingleOrDefaultAsync(x => x.TenantId == notification.TenantId && x.ReferenceId == saleId && x.DocumentType == "SALE_RECEIPT");
                    if (document?.Status == BillaresPos.Domain.Reports.GeneratedDocumentStatus.Generated && document.Content is not null)
                    {
                        attachment = document.Content;
                        fileName = document.FileName;
                    }
                }
                await emailService.SendAsync(new EmailMessage(recipient, notification.Title, notification.Body, attachment, fileName));
            }            else if (notification.Channel == NotificationChannel.WhatsApp)
            {
                var phone = await dbContext.NotificationPreferences.IgnoreQueryFilters()
                    .Where(x => x.TenantId == notification.TenantId && x.UserId == notification.RecipientUserId)
                    .Select(x => x.WhatsAppPhoneNumber).SingleOrDefaultAsync();
                if (string.IsNullOrWhiteSpace(phone)) throw new InvalidOperationException("WhatsApp recipient phone number is not configured.");
                var sendReceipt = bool.TryParse(configuration["WhatsApp:SendReceipt"], out var receiptEnabled) && receiptEnabled && notification.ReferenceType == "Sale";
                if (sendReceipt && notification.ReferenceId is Guid receiptSaleId)
                {
                    var document = await dbContext.GeneratedDocuments.IgnoreQueryFilters()
                        .SingleOrDefaultAsync(x => x.TenantId == notification.TenantId && x.ReferenceId == receiptSaleId && x.DocumentType == "SALE_RECEIPT");
                    if (document?.Status != BillaresPos.Domain.Reports.GeneratedDocumentStatus.Generated || document.Content is null)
                        throw new InvalidOperationException("El recibo PDF todavÃ­a no estÃ¡ generado.");
                    await whatsAppService.SendDocumentAsync(phone, document.Content, document.FileName, notification.Body);
                }
                else
                {
                    var messageType = configuration["WhatsApp:MessageType"] ?? "Template";
                    if (string.Equals(messageType, "Text", StringComparison.OrdinalIgnoreCase))
                    {
                        var lineBreak = Environment.NewLine;
                        var closing = notification.Type is NotificationType.StockLow or NotificationType.StockCritical or NotificationType.OutOfStock
                            ? "Revisa tu inventario y realiza la reposición antes de afectar la operación."
                            : "Gracias por tu compra.";
                        await whatsAppService.SendAsync(new WhatsAppMessage(
                            WhatsAppMessageType.Text,
                            phone,
                            TextBody: $"Billares POS{lineBreak}{lineBreak}{notification.Title}{lineBreak}{notification.Body}{lineBreak}Fecha: {notification.CreatedAt:dd/MM/yyyy HH:mm}{lineBreak}{lineBreak}{closing}"));
                    }
                    else
                    {
                        await whatsAppService.SendAsync(new WhatsAppMessage(
                            WhatsAppMessageType.Template,
                            phone,
                            configuration[$"WhatsApp:Templates:{notification.Type}"] ?? "hello_world",
                            configuration["WhatsApp:LanguageCode"] ?? "en_US",
                            Array.Empty<string>()));
                    }
                }
            }
            notification.Status = NotificationStatus.Sent;
            notification.SentAt = clock.UtcNow;
            notification.Error = null;
            await dbContext.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            notification.Status = NotificationStatus.Failed;
            notification.Error = exception.Message[..Math.Min(exception.Message.Length, 2000)];
            await dbContext.SaveChangesAsync();
            throw;
        }
    }
}
