using BillaresPos.Domain.Common;

namespace BillaresPos.Domain.Purchases;

public sealed class Purchase : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? Supplier { get; set; }
    public string? InvoiceReference { get; set; }
    public DateOnly PurchaseDate { get; set; }
    public decimal Total { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
}

