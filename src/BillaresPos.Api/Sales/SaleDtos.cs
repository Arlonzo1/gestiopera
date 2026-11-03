using BillaresPos.Domain.Sales;

namespace BillaresPos.Api.Sales;

public sealed record CreateSaleRequest(
    decimal Discount,
    string? CustomerName,
    string? Notes,
    IReadOnlyList<CreateSaleItemRequest> Items,
    IReadOnlyList<CreateSalePaymentRequest> Payments);

public sealed record CreateSaleItemRequest(
    Guid ProductId,
    decimal Quantity);

public sealed record CreateSalePaymentRequest(
    PaymentMethod Method,
    decimal Amount);

public sealed record SaleResponse(
    Guid Id,
    long SequentialNumber,
    decimal Subtotal,
    decimal Discount,
    decimal Total,
    SaleStatus Status,
    int ItemCount);

public sealed record CancelSaleRequest(string Reason);


public sealed record SaleDetailResponse(
    Guid Id,
    long SequentialNumber,
    DateTimeOffset CreatedAt,
    string Status,
    decimal Subtotal,
    decimal Discount,
    decimal Total,
    string? CashierName,
    string? CustomerName,
    string? TableName,
    int? BilliardMinutes,
    IReadOnlyList<SaleDetailItemResponse> Items,
    IReadOnlyList<SaleDetailPaymentResponse> Payments,
    DateTimeOffset? BilliardStartedAt,
    DateTimeOffset? BilliardEndedAt);

public sealed record SaleDetailItemResponse(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Subtotal,
    string ItemType);

public sealed record SaleDetailPaymentResponse(string Method, decimal Amount);
