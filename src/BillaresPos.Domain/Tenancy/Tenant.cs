namespace BillaresPos.Domain.Tenancy;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string TimeZoneId { get; set; } = "America/Bogota";
    public string Currency { get; set; } = "COP";
    public string? LogoDataUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

