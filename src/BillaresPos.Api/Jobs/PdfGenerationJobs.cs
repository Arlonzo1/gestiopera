using BillaresPos.Api.Reports;
using BillaresPos.Application.Common.Abstractions;
using BillaresPos.Domain.Reports;
using BillaresPos.Domain.Sales;
using BillaresPos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillaresPos.Api.Jobs;

public sealed class PdfGenerationJobs(IServiceScopeFactory scopeFactory)
{
    public async Task GenerateSaleReceiptAsync(Guid documentId)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pdfService = scope.ServiceProvider.GetRequiredService<IPdfService>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var document = await db.GeneratedDocuments.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == documentId);
        if (document is null || document.Status == GeneratedDocumentStatus.Generated)
            return;

        try
        {
            var sale = await db.Sales.IgnoreQueryFilters()
                .Include(x => x.Items)
                .Include(x => x.Payments)
                .SingleAsync(x => x.Id == document.ReferenceId && x.TenantId == document.TenantId);
            var tenant = await db.Tenants.SingleAsync(x => x.Id == document.TenantId);
            var cashier = await db.Users.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == sale.UserId && x.TenantId == document.TenantId);
            var timeZone = FindTimeZone(tenant.TimeZoneId);
            var model = new SalesReceiptPdfModel(
                tenant.Name,
                tenant.Currency,
                sale.SequentialNumber,
                TimeZoneInfo.ConvertTime(sale.CreatedAt, timeZone),
                cashier?.FullName ?? "Usuario",
                sale.CustomerName,
                sale.Items.OrderBy(x => x.Id).Select(x => new SalesReceiptPdfItem(
                    x.DescriptionSnapshot, x.Quantity, x.UnitPrice, x.Subtotal, x.ItemType == SaleItemType.BilliardTime)).ToList(),
                sale.Subtotal,
                sale.Discount,
                sale.Total,
                sale.Payments.Select(x => new SalesReceiptPdfPayment(PaymentName(x.Method), x.Amount)).ToList());

            document.Content = pdfService.CreateSalesReceipt(model);
            document.Status = GeneratedDocumentStatus.Generated;
            document.GeneratedAt = clock.UtcNow;
            document.Error = null;
            await db.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            document.Status = GeneratedDocumentStatus.Failed;
            document.Error = exception.Message[..Math.Min(exception.Message.Length, 2000)];
            await db.SaveChangesAsync();
            throw;
        }
    }

    public async Task GenerateDailyReportsAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenants = await db.Tenants.AsNoTracking().Where(x => x.IsActive).ToListAsync();
        foreach (var tenant in tenants)
        {
            var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, FindTimeZone(tenant.TimeZoneId)).DateTime).AddDays(-1);
            await GenerateReportAsync(scope.ServiceProvider, tenant, localDate, localDate, "DAILY_REPORT", $"reporte-diario-{localDate:yyyyMMdd}.pdf");
        }
    }

    public async Task GenerateMonthlyReportsAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenants = await db.Tenants.AsNoTracking().Where(x => x.IsActive).ToListAsync();
        foreach (var tenant in tenants)
        {
            var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, FindTimeZone(tenant.TimeZoneId)).DateTime);
            var monthEnd = new DateOnly(localDate.Year, localDate.Month, 1).AddDays(-1);
            var monthStart = new DateOnly(monthEnd.Year, monthEnd.Month, 1);
            await GenerateReportAsync(scope.ServiceProvider, tenant, monthStart, monthEnd, "MONTHLY_REPORT", $"reporte-mensual-{monthStart:yyyyMM}.pdf");
        }
    }

    public async Task RetryFailedPdfJobsAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var failedReceipts = await db.GeneratedDocuments.IgnoreQueryFilters()
            .Where(x => x.Status == GeneratedDocumentStatus.Failed && x.DocumentType == "SALE_RECEIPT")
            .OrderBy(x => x.CreatedAt)
            .Take(50)
            .Select(x => x.Id)
            .ToListAsync();
        foreach (var id in failedReceipts)
            await GenerateSaleReceiptAsync(id);
    }

    private async Task GenerateReportAsync(IServiceProvider services, Domain.Tenancy.Tenant tenant, DateOnly from, DateOnly to, string documentType, string fileName)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var pdfService = services.GetRequiredService<IPdfService>();
        var clock = services.GetRequiredService<IClock>();
        var existing = await db.GeneratedDocuments.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.TenantId == tenant.Id && x.DocumentType == documentType && x.FileName == fileName);
        if (existing?.Status == GeneratedDocumentStatus.Generated)
            return;

        var document = existing ?? new GeneratedDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            DocumentType = documentType,
            FileName = fileName,
            CreatedAt = clock.UtcNow
        };
        document.Status = GeneratedDocumentStatus.Pending;
        document.Error = null;
        if (existing is null)
            db.GeneratedDocuments.Add(document);
        await db.SaveChangesAsync();

        try
        {
            var timeZone = FindTimeZone(tenant.TimeZoneId);
            var current = await ReportEndpoints.BuildMetricsAsync(db, tenant.Id, from, to, timeZone, CancellationToken.None);
            var days = to.DayNumber - from.DayNumber + 1;
            var previousTo = from.AddDays(-1);
            var previousFrom = previousTo.AddDays(-(days - 1));
            var previous = await ReportEndpoints.BuildMetricsAsync(db, tenant.Id, previousFrom, previousTo, timeZone, CancellationToken.None);
            document.Content = pdfService.CreateMonthlyReport(new MonthlyReportPdfModel(
                tenant.Name, tenant.Currency, from, to, ToPdfMetrics(current), ToPdfMetrics(previous)));
            document.Status = GeneratedDocumentStatus.Generated;
            document.GeneratedAt = clock.UtcNow;
            await db.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            document.Status = GeneratedDocumentStatus.Failed;
            document.Error = exception.Message[..Math.Min(exception.Message.Length, 2000)];
            await db.SaveChangesAsync();
            throw;
        }
    }

    private static ReportPdfMetrics ToPdfMetrics(ReportMetrics metrics) => new(
        metrics.GrossSales, metrics.ProductRevenue, metrics.BilliardRevenue, metrics.CostOfGoodsSold,
        metrics.GrossProfit, metrics.Expenses, metrics.OperatingResult, metrics.CashPayments,
        metrics.ElectronicPayments, metrics.BilliardHours, metrics.CompletedSales, metrics.CancelledSales);

    private static string PaymentName(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Efectivo",
        PaymentMethod.Nequi => "Nequi",
        PaymentMethod.Daviplata => "Daviplata",
        PaymentMethod.Card => "Tarjeta",
        PaymentMethod.BankTransfer => "Transferencia bancaria",
        _ => "Otro"
    };

    private static TimeZoneInfo FindTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) when (id == "America/Bogota")
        { return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time"); }
    }
}