using BillaresPos.Application.Common.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BillaresPos.Infrastructure.Reporting;

public sealed class QuestPdfService : IPdfService
{
    public QuestPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] CreateSalesReceipt(SalesReceiptPdfModel model)
    {
        var document = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A5);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontSize(9));
            page.Content().Column(column =>
            {
                column.Spacing(8);
                column.Item().AlignCenter().Text(model.BusinessName).Bold().FontSize(16);
                column.Item().AlignCenter().Text("COMPROBANTE INTERNO DE VENTA").FontSize(8);
                column.Item().LineHorizontal(1);
                column.Item().Text($"Venta #{model.SaleNumber}").Bold();
                column.Item().Text($"Fecha: {model.CreatedAt:dd/MM/yyyy HH:mm}");
                column.Item().Text($"Cajero: {model.CashierName}");
                if (!string.IsNullOrWhiteSpace(model.CustomerName)) column.Item().Text($"A nombre de: {model.CustomerName}");
                column.Item().LineHorizontal(1);

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(1.4f);
                        columns.RelativeColumn(1.5f);
                    });
                    table.Header(header =>
                    {
                        header.Cell().Text("Producto").Bold();
                        header.Cell().AlignRight().Text("Cant.").Bold();
                        header.Cell().AlignRight().Text("Precio").Bold();
                        header.Cell().AlignRight().Text("Total").Bold();
                    });
                    foreach (var item in model.Items)
                    {
                        table.Cell().Text(item.IsBilliardTime ? $"{item.Description} (tiempo)": item.Description);
                        table.Cell().AlignRight().Text(item.Quantity.ToString("0.###"));
                        table.Cell().AlignRight().Text(Money(item.UnitPrice, model.Currency));
                        table.Cell().AlignRight().Text(Money(item.Subtotal, model.Currency));
                    }
                });

                column.Item().LineHorizontal(1);
                column.Item().AlignRight().Text($"Subtotal: {Money(model.Subtotal, model.Currency)}");
                column.Item().AlignRight().Text($"Descuento: {Money(model.Discount, model.Currency)}");
                column.Item().AlignRight().Text($"TOTAL: {Money(model.Total, model.Currency)}").Bold().FontSize(12);
                column.Item().Text("Medios de pago").Bold();
                foreach (var payment in model.Payments)
                    column.Item().Text($"{payment.Method}: {Money(payment.Amount, model.Currency)}");
                column.Item().PaddingTop(8).AlignCenter().Text("Documento interno. No es factura electrónica DIAN.").FontSize(7).Italic();
            });
        }));

        return document.GeneratePdf();
    }

    public byte[] CreateMonthlyReport(MonthlyReportPdfModel model)
    {
        var document = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(10));
            page.Content().Column(column =>
            {
                column.Spacing(10);
                column.Item().Text(model.BusinessName).Bold().FontSize(20);
                column.Item().Text($"Reporte de operación: {model.From:dd/MM/yyyy} - {model.To:dd/MM/yyyy}");
                column.Item().Text("Comparativo contra el período anterior").FontSize(9).Italic();
                column.Item().LineHorizontal(1);
                AddMetrics(column, "Período actual", model.Current, model.Currency);
                column.Item().PaddingTop(12);
                AddMetrics(column, "Período anterior", model.Previous, model.Currency);
            });
        }));

        return document.GeneratePdf();
    }

    private static void AddMetrics(ColumnDescriptor column, string title, ReportPdfMetrics metrics, string currency)
    {
        column.Item().Text(title).Bold().FontSize(13);
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.RelativeColumn(1);
                columns.RelativeColumn(2);
                columns.RelativeColumn(1);
            });
            AddRow(table, "Ventas brutas", Money(metrics.GrossSales, currency), "Ventas completadas", metrics.CompletedSales.ToString());
            AddRow(table, "Ventas de productos", Money(metrics.ProductRevenue, currency), "Ventas de billar", Money(metrics.BilliardRevenue, currency));
            AddRow(table, "Costo de ventas", Money(metrics.CostOfGoodsSold, currency), "Utilidad bruta", Money(metrics.GrossProfit, currency));
            AddRow(table, "Gastos", Money(metrics.Expenses, currency), "Resultado operativo", Money(metrics.OperatingResult, currency));
            AddRow(table, "Efectivo", Money(metrics.CashPayments, currency), "Electrónico", Money(metrics.ElectronicPayments, currency));
            AddRow(table, "Horas de billar", metrics.BilliardHours.ToString("0.##"), "Ventas canceladas", metrics.CancelledSales.ToString());
        });
    }

    private static void AddRow(TableDescriptor table, string label1, string value1, string label2, string value2)
    {
        table.Cell().Text(label1);
        table.Cell().AlignRight().Text(value1).Bold();
        table.Cell().Text(label2);
        table.Cell().AlignRight().Text(value2).Bold();
    }

    private static string Money(decimal value, string currency) => $"{currency} {value:N0}";
}
