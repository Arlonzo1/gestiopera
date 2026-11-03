using BillaresPos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillaresPos.Api.Tenancy;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext, CurrentTenant currentTenant)
    {
        var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
        if (Guid.TryParse(tenantClaim, out var tenantId))
        {
            var tenant = await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tenantId && x.IsActive, context.RequestAborted);
            if (tenant is not null)
            {
                currentTenant.Set(tenant.Id, tenant.Slug, tenant.TimeZoneId, tenant.Currency);
            }
        }

        await next(context);
    }
}
