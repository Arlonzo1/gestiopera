using BillaresPos.Domain.CashManagement;
using BillaresPos.Domain.Sales;

namespace BillaresPos.Api.CashManagement;

public sealed record OpenCashShiftRequest(decimal OpeningCash);
public sealed record CloseCashShiftRequest(decimal CountedCash);
public sealed record CreateExpenseRequest(decimal Amount, ExpensePaymentMethod PaymentMethod, string Category, string? Description);
public sealed record CashShiftResponse(Guid Id, Guid UserId, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt, decimal OpeningCash, decimal ExpectedCash, decimal? CountedCash, decimal? Difference, CashShiftStatus Status);
public sealed record CashShiftSummaryResponse(Guid Id, decimal OpeningCash, decimal CashSales, decimal CashExpenses, decimal ExpectedCash, CashShiftStatus Status);
public sealed record CashShiftHistoryResponse(Guid Id, Guid UserId, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt, decimal OpeningCash, decimal CashSales, decimal CashExpenses, decimal ExpectedCash, decimal? CountedCash, decimal? Difference, CashShiftStatus Status);
public sealed record ExpenseResponse(Guid Id, decimal Amount, ExpensePaymentMethod PaymentMethod, string Category, string? Description, DateTimeOffset CreatedAt);
