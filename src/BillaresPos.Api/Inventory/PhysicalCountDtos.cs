namespace BillaresPos.Api.Inventory;

public sealed record ConfirmPhysicalCountRequest(IReadOnlyList<PhysicalCountItemRequest> Items);
public sealed record PhysicalCountItemRequest(Guid ProductId, decimal PhysicalQuantity);
public sealed record PhysicalCountResponse(Guid Id, DateTimeOffset CountedAt, int ItemCount, int AdjustedItemCount);
