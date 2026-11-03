using BillaresPos.Domain.Billiards;
using BillaresPos.Api.Sales;
using BillaresPos.Domain.Sales;

namespace BillaresPos.Api.Billiards;

public sealed record CreateBilliardTableRequest(string Name, decimal HourlyRate, int BillingIncrementMinutes);

public sealed record UpdateBilliardTableRequest(string Name, decimal HourlyRate, int BillingIncrementMinutes);

public sealed record BilliardTableResponse(
    Guid Id,
    string Name,
    decimal HourlyRate,
    int BillingIncrementMinutes,
    BilliardTableStatus Status,
    Guid? ActiveSessionId,
    DateTimeOffset? StartedAtUtc,
    long TotalPausedSeconds,
    DateTimeOffset? PausedAtUtc);

public sealed record AddBilliardConsumptionRequest(Guid ProductId, decimal Quantity);

public sealed record CloseBilliardSessionRequest(IReadOnlyList<CreateSalePaymentRequest> Payments, string? CustomerName);

public sealed record BilliardSessionResponse(
    Guid SessionId,
    Guid TableId,
    BilliardSessionStatus Status,
    long ElapsedSeconds,
    int BilledMinutes,
    decimal BilliardCharge,
    decimal ConsumptionTotal,
    decimal Total,
    Guid? SaleId);



public sealed record BilliardConsumptionResponse(Guid Id, Guid ProductId, string DescriptionSnapshot, decimal Quantity, decimal UnitPrice, decimal Subtotal);

public sealed record BilliardSessionDetailResponse(Guid SessionId, Guid TableId, BilliardSessionStatus Status, long ElapsedSeconds, int BilledMinutes, decimal BilliardCharge, decimal ConsumptionTotal, decimal Total, IReadOnlyList<BilliardConsumptionResponse> Consumptions);

