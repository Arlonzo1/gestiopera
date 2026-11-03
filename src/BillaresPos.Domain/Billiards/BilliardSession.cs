using BillaresPos.Domain.Common;

namespace BillaresPos.Domain.Billiards;

public sealed class BilliardSession : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BilliardTableId { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }
    public long TotalPausedSeconds { get; set; }
    public DateTimeOffset? PausedAtUtc { get; set; }
    public decimal HourlyRateSnapshot { get; set; }
    public int BillingIncrementMinutesSnapshot { get; set; }
    public Guid OpenedByUserId { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public BilliardSessionStatus Status { get; set; } = BilliardSessionStatus.Active;
    public ICollection<BilliardConsumption> Consumptions { get; set; } = new List<BilliardConsumption>();
}

