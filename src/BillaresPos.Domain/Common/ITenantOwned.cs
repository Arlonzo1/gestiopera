namespace BillaresPos.Domain.Common;

public interface ITenantOwned
{
    Guid TenantId { get; }
}
