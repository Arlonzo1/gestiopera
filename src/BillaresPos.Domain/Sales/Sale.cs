using BillaresPos.Domain.Common;

namespace BillaresPos.Domain.Sales;

public sealed class Sale : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public long SequentialNumber { get; set; }
    public Guid? CashShiftId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public SaleStatus Status { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public string? CustomerName { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public string? CancellationReason { get; set; }
    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
    public ICollection<SalePayment> Payments { get; set; } = new List<SalePayment>();
}

