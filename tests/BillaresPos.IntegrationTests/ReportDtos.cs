namespace BillaresPos.IntegrationTests;

public sealed record ReportResponse(
    DateOnly From,
    DateOnly To,
    DateOnly PreviousFrom,
    DateOnly PreviousTo,
    ReportMetrics Current,
    ReportMetrics Previous);

public sealed record ReportMetrics(
    decimal GrossSales,
    decimal ProductRevenue,
    decimal BilliardRevenue,
    decimal CostOfGoodsSold,
    decimal GrossProfit,
    decimal Expenses,
    decimal OperatingResult,
    decimal CashPayments,
    decimal ElectronicPayments,
    decimal BilliardHours,
    int CompletedSales,
    int CancelledSales,
    IReadOnlyList<object> PaymentBreakdown,
    IReadOnlyList<object> SalesByDay,
    IReadOnlyList<object> SalesByEmployee,
    IReadOnlyList<object> SalesByProduct);
