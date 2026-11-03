using BillaresPos.Domain.Common;

namespace BillaresPos.Domain.Billiards;

public sealed class BilliardTable : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
    public int BillingIncrementMinutes { get; set; }
    public BilliardTableKind Kind { get; set; } = BilliardTableKind.Billiard;
    public BilliardTableStatus Status { get; set; } = BilliardTableStatus.Available;
    public bool IsActive { get; set; } = true;
}

