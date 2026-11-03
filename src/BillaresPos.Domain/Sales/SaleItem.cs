using BillaresPos.Domain.Common;

namespace BillaresPos.Domain.Sales;

public sealed class SaleItem : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SaleId { get; set; }
    public Guid? ProductId { get; set; }
    public string DescriptionSnapshot { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitCostAtSale { get; set; }
    public decimal Subtotal { get; set; }
    public SaleItemType ItemType { get; set; }
    public Sale Sale { get; set; } = null!;
}

