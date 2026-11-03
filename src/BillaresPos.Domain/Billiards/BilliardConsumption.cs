using BillaresPos.Domain.Common;

namespace BillaresPos.Domain.Billiards;

public sealed class BilliardConsumption : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BilliardSessionId { get; set; }
    public Guid ProductId { get; set; }
    public string DescriptionSnapshot { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal UnitCostAtConsumption { get; set; }
    public decimal Subtotal { get; set; }
    public Guid AddedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public BilliardSession BilliardSession { get; set; } = null!;
}

