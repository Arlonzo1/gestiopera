using BillaresPos.Domain.Inventory;

namespace BillaresPos.Api.Products;

public sealed record CreateProductRequest(
    string Name,
    string? Sku,
    Guid? CategoryId,
    decimal UnitCost,
    decimal SalePrice,
    decimal MinimumStock,
    decimal CriticalStock);

public sealed record UpdateProductRequest(
    string Name,
    string? Sku,
    decimal UnitCost,
    decimal SalePrice,
    decimal MinimumStock,
    decimal CriticalStock);
public sealed record ProductResponse(
    Guid Id,
    string Name,
    string? Sku,
    Guid? CategoryId,
    decimal SalePrice,
    decimal AverageCost,
    decimal CurrentStock,
    decimal MinimumStock,
    decimal CriticalStock,
    bool IsActive);

public sealed record AdjustInventoryRequest(
    decimal Quantity,
    InventoryMovementType MovementType,
    decimal? UnitCost,
    string? Notes);

