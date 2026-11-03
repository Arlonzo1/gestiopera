using BillaresPos.Domain.Common;

namespace BillaresPos.Domain.CashManagement;

public sealed class Expense : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CashShiftId { get; set; }
    public decimal Amount { get; set; }
    public ExpensePaymentMethod PaymentMethod { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
