using BillaresPos.Application.Common.Abstractions;

namespace BillaresPos.Api.Tenancy;

public sealed class CurrentTenant : ICurrentTenant
{
    public Guid? TenantId { get; private set; }
    public string? Slug { get; private set; }
    public string TimeZoneId { get; private set; } = "America/Bogota";
    public string Currency { get; private set; } = "COP";
    public bool IsResolved => TenantId.HasValue;

    public void Set(Guid tenantId, string? slug, string timeZoneId, string currency)
    {
        TenantId = tenantId;
        Slug = slug;
        TimeZoneId = timeZoneId;
        Currency = currency;
    }
}
