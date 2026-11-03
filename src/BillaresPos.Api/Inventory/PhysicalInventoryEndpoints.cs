using BillaresPos.Application.Common.Abstractions;
using BillaresPos.Application.Common.Exceptions;
using BillaresPos.Domain.Inventory;
using BillaresPos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillaresPos.Api.Inventory;

public static class PhysicalInventoryEndpoints
{
    public static void MapPhysicalInventoryEndpoints(this WebApplication app)
    {
        app.MapPost("/api/inventory/physical-counts", ConfirmAsync)
            .RequireAuthorization("AdministratorOrOwner")
            .WithTags("Inventory");
    }

    private static async Task<IResult> ConfirmAsync(
        ConfirmPhysicalCountRequest request,
        AppDbContext db,
        ICurrentTenant tenant,
        ICurrentUser user,
        IClock clock,
        CancellationToken ct)
    {
        if (tenant.TenantId is null || user.UserId is null || request.Items is null || request.Items.Count == 0 || request.Items.Any(x => x.PhysicalQuantity < 0 || decimal.Truncate(x.PhysicalQuantity) != x.PhysicalQuantity))
            throw new ValidationAppException("El conteo fisico no es valido.", new Dictionary<string, string[]> { ["items"] = ["Debe incluir productos y cantidades no negativas."] });

        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var productIds = request.Items.Select(x => x.ProductId).Distinct().ToArray();
        var products = await db.Products.Where(x => productIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, ct);
        if (products.Count != productIds.Length) throw new NotFoundException("Uno o mas productos no existen en el tenant actual.");

        var count = new PhysicalInventoryCount { Id = Guid.NewGuid(), TenantId = tenant.TenantId.Value, CountedByUserId = user.UserId.Value, CountedAt = clock.UtcNow };
        var adjusted = 0;
        foreach (var item in request.Items)
        {
            var product = products[item.ProductId];
            var difference = item.PhysicalQuantity - product.CurrentStock;
            count.Items.Add(new PhysicalInventoryCountItem { Id = Guid.NewGuid(), TenantId = count.TenantId, ProductId = product.Id, SystemQuantity = product.CurrentStock, PhysicalQuantity = item.PhysicalQuantity, Difference = difference });
            if (difference == 0) continue;
            product.CurrentStock = item.PhysicalQuantity;
            db.InventoryMovements.Add(new InventoryMovement { Id = Guid.NewGuid(), TenantId = count.TenantId, ProductId = product.Id, MovementType = InventoryMovementType.PhysicalCountAdjustment, Quantity = difference, ReferenceType = "PhysicalInventoryCount", ReferenceId = count.Id, Notes = "Conteo fisico", CreatedByUserId = user.UserId.Value, CreatedAt = count.CountedAt });
            adjusted++;
        }
        db.PhysicalInventoryCounts.Add(count);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Created($"/api/inventory/physical-counts/{count.Id}", new PhysicalCountResponse(count.Id, count.CountedAt, count.Items.Count, adjusted));
    }
}

