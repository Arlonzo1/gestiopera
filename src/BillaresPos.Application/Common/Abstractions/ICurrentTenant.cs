namespace BillaresPos.Application.Common.Abstractions;

public interface ICurrentTenant
{
    Guid? TenantId { get; }
    string? Slug { get; }
    string TimeZoneId { get; }
    string Currency { get; }
    bool IsResolved { get; }
}
