using BillaresPos.Domain.Common;

namespace BillaresPos.Domain.Sales;

public sealed class SalePayment : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SaleId { get; set; }
    public PaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public Sale Sale { get; set; } = null!;
}

