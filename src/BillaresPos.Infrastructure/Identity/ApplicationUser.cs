using BillaresPos.Domain.Common;
using BillaresPos.Domain.Tenancy;
using Microsoft.AspNetCore.Identity;

namespace BillaresPos.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
