using BillaresPos.Domain.Common;

namespace BillaresPos.Domain.Purchases;

public sealed class PurchaseItem : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PurchaseId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Subtotal { get; set; }
    public Purchase Purchase { get; set; } = null!;
}

