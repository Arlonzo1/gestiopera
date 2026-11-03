using BillaresPos.Domain.Common;

namespace BillaresPos.Domain.Notifications;

public enum SaleNotificationMode
{
    Never,
    AllSales,
    AboveAmount
}

public sealed class NotificationPreference : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public SaleNotificationMode SaleNotificationMode { get; set; } = SaleNotificationMode.AllSales;
    public decimal SaleNotificationAmount { get; set; }
    public bool PushEnabled { get; set; } = true;
    public bool WhatsAppEnabled { get; set; }
    public string? WhatsAppPhoneNumber { get; set; }
    public string? EmailAddress { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
