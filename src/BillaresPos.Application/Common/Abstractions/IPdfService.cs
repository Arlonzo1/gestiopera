namespace BillaresPos.Application.Common.Abstractions;

public interface IPdfService
{
    byte[] CreateSalesReceipt(SalesReceiptPdfModel model);
    byte[] CreateMonthlyReport(MonthlyReportPdfModel model);
}

public sealed record SalesReceiptPdfModel(
    string BusinessName,
    string Currency,
    long SaleNumber,
    DateTimeOffset CreatedAt,
    string CashierName,
    string? CustomerName,
    IReadOnlyList<SalesReceiptPdfItem> Items,
    decimal Subtotal,
    decimal Discount,
    decimal Total,
    IReadOnlyList<SalesReceiptPdfPayment> Payments);

public sealed record SalesReceiptPdfItem(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Subtotal,
    bool IsBilliardTime);

public sealed record SalesReceiptPdfPayment(string Method, decimal Amount);

public sealed record MonthlyReportPdfModel(
    string BusinessName,
    string Currency,
    DateOnly From,
    DateOnly To,
    ReportPdfMetrics Current,
    ReportPdfMetrics Previous);

public sealed record ReportPdfMetrics(
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
    int CancelledSales);
