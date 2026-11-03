using BillaresPos.Domain.Common;

namespace BillaresPos.Domain.CashManagement;

public sealed class CashShift : ITenantOwned
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public decimal OpeningCash { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? Difference { get; set; }
    public CashShiftStatus Status { get; set; } = CashShiftStatus.Open;
}
