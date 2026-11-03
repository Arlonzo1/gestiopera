using BillaresPos.Domain.Common;

namespace BillaresPos.Domain.Notifications;

public enum NotificationChannel
{
    InApp,
    Push,
    WhatsApp,
    Email
}

public enum NotificationStatus
{
    Pending,
    Sent,
    Failed
}

public enum NotificationType
{
    SaleCompleted,
    SaleCancelled,
    PurchaseCompleted,
    StockLow,
    StockCritical,
    OutOfStock,
    CashDifference
}

public sealed class Notification : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RecipientUserId { get; set; }
    public NotificationChannel Channel { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public NotificationStatus Status { get; set; }
    public string? ExternalMessageId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public string? Error { get; set; }
}


