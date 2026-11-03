using BillaresPos.Domain.Notifications;
using BillaresPos.Infrastructure.Identity;
using BillaresPos.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BillaresPos.Api.Notifications;

public sealed class NotificationService(AppDbContext dbContext, IConfiguration configuration)
{
    public async Task<IReadOnlyList<Guid>> CreateForOwnersAsync(
        Guid tenantId, NotificationType type, string title, string body, string referenceType,
        Guid referenceId, DateTimeOffset createdAt, decimal? saleAmount, CancellationToken cancellationToken)
    {
        var ownerRoleId = await dbContext.Roles.Where(x => x.NormalizedName == "OWNER").Select(x => x.Id).SingleAsync(cancellationToken);
        var ownerIds = await (from userRole in dbContext.Set<IdentityUserRole<Guid>>()
            join user in dbContext.Users.IgnoreQueryFilters() on userRole.UserId equals user.Id
            where userRole.RoleId == ownerRoleId && user.TenantId == tenantId && user.IsActive
            select user.Id).Distinct().ToListAsync(cancellationToken);
        var ownerEmails = await dbContext.Users.IgnoreQueryFilters()
            .Where(x => ownerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Email, cancellationToken);
        var emailEnabled = bool.Parse(configuration["Email:Enabled"] ?? "false");
        var preferences = await dbContext.NotificationPreferences.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && ownerIds.Contains(x.UserId)).ToDictionaryAsync(x => x.UserId, cancellationToken);
        var deviceUserIds = await dbContext.PushDeviceTokens.IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.IsActive && ownerIds.Contains(x.UserId))
            .Select(x => x.UserId).Distinct().ToListAsync(cancellationToken);
        var recipients = ownerIds.Where(ownerId => ShouldReceive(type, saleAmount, preferences.GetValueOrDefault(ownerId))).ToList();
        var notifications = new List<Notification>();
        foreach (var ownerId in recipients)
        {
            notifications.Add(CreateNotification(tenantId, ownerId, NotificationChannel.InApp, type, title, body, referenceType, referenceId, createdAt));
            var preference = preferences.GetValueOrDefault(ownerId);
            if (deviceUserIds.Contains(ownerId) && (preference is null || preference.PushEnabled))
                notifications.Add(CreateNotification(tenantId, ownerId, NotificationChannel.Push, type, title, body, referenceType, referenceId, createdAt));
            var emailAddress = preferences.GetValueOrDefault(ownerId)?.EmailAddress ?? ownerEmails.GetValueOrDefault(ownerId);
            if (emailEnabled && !string.IsNullOrWhiteSpace(emailAddress))
                notifications.Add(CreateNotification(tenantId, ownerId, NotificationChannel.Email, type, title, body, referenceType, referenceId, createdAt));
            var whatsappAllowed = type is NotificationType.SaleCompleted or NotificationType.SaleCancelled
                or NotificationType.PurchaseCompleted or NotificationType.StockLow or NotificationType.StockCritical
                or NotificationType.OutOfStock or NotificationType.CashDifference;
            if (whatsappAllowed && preference is { WhatsAppEnabled: true } && !string.IsNullOrWhiteSpace(preference.WhatsAppPhoneNumber))
                notifications.Add(CreateNotification(tenantId, ownerId, NotificationChannel.WhatsApp, type, title, body, referenceType, referenceId, createdAt));
        }
        dbContext.Notifications.AddRange(notifications);
        return notifications.Select(x => x.Id).ToList();
    }

    private static bool ShouldReceive(NotificationType type, decimal? saleAmount, NotificationPreference? preference)
    {
        if (type != NotificationType.SaleCompleted || preference is null) return true;
        return preference.SaleNotificationMode switch
        {
            SaleNotificationMode.Never => false,
            SaleNotificationMode.AboveAmount => saleAmount is not null && saleAmount >= preference.SaleNotificationAmount,
            _ => true
        };
    }

    private static Notification CreateNotification(
        Guid tenantId, Guid recipientUserId, NotificationChannel channel, NotificationType type,
        string title, string body, string referenceType, Guid referenceId, DateTimeOffset createdAt) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, RecipientUserId = recipientUserId, Channel = channel,
        Type = type, Title = title, Body = body, ReferenceType = referenceType, ReferenceId = referenceId,
        Status = NotificationStatus.Pending, CreatedAt = createdAt
    };
}