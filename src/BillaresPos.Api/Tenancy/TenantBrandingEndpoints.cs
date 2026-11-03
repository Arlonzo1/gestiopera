using BillaresPos.Api.Errors;
using BillaresPos.Application.Common.Abstractions;
using BillaresPos.Application.Common.Exceptions;
using BillaresPos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillaresPos.Api.Tenancy;

public sealed record TenantBrandingResponse(string Name, string? LogoDataUrl);
public sealed record UpdateTenantBrandingRequest(string Name, string? LogoDataUrl);

public static class TenantBrandingEndpoints
{
    public static void MapTenantBrandingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tenants/branding").RequireAuthorization().WithTags("Tenancy");
        group.MapGet("", GetAsync);
        group.MapPut("", UpdateAsync).RequireAuthorization("AdministratorOrOwner");
    }

    private static async Task<IResult> GetAsync(AppDbContext db, ICurrentTenant tenant, CancellationToken ct)
    {
        if (tenant.TenantId is null) throw new ForbiddenException("Se requiere un tenant autenticado.");
        var branding = await db.Tenants.AsNoTracking()
            .Where(x => x.Id == tenant.TenantId.Value && x.IsActive)
            .Select(x => new TenantBrandingResponse(x.Name, x.LogoDataUrl))
            .SingleOrDefaultAsync(ct);
        return branding is null ? Results.NotFound() : Results.Ok(branding);
    }

    private static async Task<IResult> UpdateAsync(UpdateTenantBrandingRequest request, AppDbContext db, ICurrentTenant tenant, CancellationToken ct)
    {
        if (tenant.TenantId is null) throw new ForbiddenException("Se requiere un tenant autenticado.");
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 160)
            throw new ValidationAppException("La marca no es válida.", new Dictionary<string, string[]> { ["name"] = ["El nombre es obligatorio y no puede superar 160 caracteres."] });
        var logo = request.LogoDataUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(logo) && (logo.Length > 1000000 ||
            !(logo.StartsWith("data:image/png;", StringComparison.OrdinalIgnoreCase) || logo.StartsWith("data:image/jpeg;", StringComparison.OrdinalIgnoreCase) || logo.StartsWith("data:image/webp;", StringComparison.OrdinalIgnoreCase))))
            throw new ValidationAppException("El logo no es válido.", new Dictionary<string, string[]> { ["logoDataUrl"] = ["Usa una imagen PNG, JPG o WEBP de máximo 1 MB."] });
        var tenantEntity = await db.Tenants.SingleOrDefaultAsync(x => x.Id == tenant.TenantId.Value && x.IsActive, ct);
        if (tenantEntity is null) return Results.NotFound();
        tenantEntity.Name = name;
        tenantEntity.LogoDataUrl = string.IsNullOrWhiteSpace(logo) ? null : logo;
        tenantEntity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new TenantBrandingResponse(tenantEntity.Name, tenantEntity.LogoDataUrl));
    }
}

