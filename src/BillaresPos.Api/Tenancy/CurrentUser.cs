using System.Security.Claims;
using BillaresPos.Application.Common.Abstractions;

namespace BillaresPos.Api.Tenancy;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;
    public Guid? UserId => Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub"), out var id) ? id : null;
    public Guid? TenantId => Guid.TryParse(User?.FindFirstValue("tenant_id"), out var id) ? id : null;
    public string? Email => User?.FindFirstValue(ClaimTypes.Email) ?? User?.FindFirstValue("email");
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;
    public bool IsInRole(string role) => User?.IsInRole(role) == true;
}
