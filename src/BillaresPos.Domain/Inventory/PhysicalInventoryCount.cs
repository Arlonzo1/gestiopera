using BillaresPos.Domain.Common;

namespace BillaresPos.Domain.Inventory;

public sealed class PhysicalInventoryCount : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CountedByUserId { get; set; }
    public DateTimeOffset CountedAt { get; set; }
    public ICollection<PhysicalInventoryCountItem> Items { get; set; } = new List<PhysicalInventoryCountItem>();
}

public sealed class PhysicalInventoryCountItem : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PhysicalInventoryCountId { get; set; }
    public Guid ProductId { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal PhysicalQuantity { get; set; }
    public decimal Difference { get; set; }
    public PhysicalInventoryCount Count { get; set; } = null!;
}
