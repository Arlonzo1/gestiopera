namespace BillaresPos.Api.Purchases;

public sealed record CreatePurchaseRequest(
    string? Supplier,
    string? InvoiceReference,
    DateOnly PurchaseDate,
    IReadOnlyList<CreatePurchaseItemRequest> Items);

public sealed record CreatePurchaseItemRequest(
    Guid ProductId,
    decimal Quantity,
    decimal UnitCost);

public sealed record PurchaseResponse(
    Guid Id,
    DateOnly PurchaseDate,
    decimal Total,
    int ItemCount);

