using BillaresPos.Domain.Sales;

namespace BillaresPos.Api.Dashboard;

public sealed record DashboardResponse(
    DateOnly BusinessDate,
    decimal GrossSales,
    decimal ProductRevenue,
    decimal BilliardRevenue,
    decimal Expenses,
    decimal ExpectedCash,
    int ActiveTables,
    int TotalTables,
    int LowStockProducts,
    IReadOnlyList<DashboardPaymentBreakdown> PaymentBreakdown,
    IReadOnlyList<DashboardRecentSale> RecentSales,
    IReadOnlyList<DashboardLowStockProduct> LowStockItems);

public sealed record DashboardPaymentBreakdown(PaymentMethod Method, decimal Amount);

public sealed record DashboardRecentSale(
    Guid Id,
    long SequentialNumber,
    DateTimeOffset CreatedAt,
    decimal Total,
    SaleStatus Status,
    Guid UserId,
    string? BilliardTableName,
    int? BilliardMinutes,
    decimal? BilliardAmount);

public sealed record DashboardLowStockProduct(
    Guid Id,
    string Name,
    decimal CurrentStock,
    decimal MinimumStock,
    decimal CriticalStock);
