using BillaresPos.Domain.Sales;

namespace BillaresPos.Api.Reports;

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
    IReadOnlyList<ReportPaymentRow> PaymentBreakdown,
    IReadOnlyList<ReportDailyRow> SalesByDay,
    IReadOnlyList<ReportEmployeeRow> SalesByEmployee,
    IReadOnlyList<ReportProductRow> SalesByProduct,
    IReadOnlyList<ReportHourRow> SalesByHour,
    IReadOnlyList<ReportTableRow> BilliardByTable);

public sealed record ReportPaymentRow(PaymentMethod Method, decimal Amount);
public sealed record ReportDailyRow(DateOnly Date, decimal Amount);
public sealed record ReportEmployeeRow(Guid UserId, string EmployeeName, decimal Amount, int SalesCount);
public sealed record ReportHourRow(int Hour, decimal Amount, int SalesCount);
public sealed record ReportTableRow(Guid TableId, string TableName, decimal Hours, int Sessions);
public sealed record ReportProductRow(Guid? ProductId, string ProductName, decimal Quantity, decimal Revenue, decimal Cost, decimal GrossProfit);