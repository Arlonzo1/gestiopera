using BillaresPos.Application.Common.Abstractions;
using BillaresPos.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BillaresPos.Api.Reports;

public static class ReportPdfEndpoints
{
    public static void MapReportPdfEndpoints(this WebApplication app)
    {
        app.MapGet("/api/reports/pdf", GetPdfAsync)
            .RequireAuthorization("AdministratorOrOwner")
            .WithTags("Reports")
            .Produces(200, contentType: "application/pdf");
    }

    private static async Task<IResult> GetPdfAsync(
        DateOnly? from,
        DateOnly? to,
        AppDbContext db,
        BillaresPos.Application.Common.Abstractions.ICurrentTenant currentTenant,
        BillaresPos.Application.Common.Abstractions.IClock clock,
        IPdfService pdfService,
        CancellationToken ct)
    {
        var result = await ReportEndpoints.GetAsync(from, to, db, currentTenant, clock, ct);
        if (result is not IValueHttpResult valueResult || valueResult.Value is not ReportResponse report)
            return result;

        var tenant = await db.Tenants.AsNoTracking().SingleAsync(x => x.Id == currentTenant.TenantId!.Value, ct);
        var model = new MonthlyReportPdfModel(
            tenant.Name,
            tenant.Currency,
            report.From,
            report.To,
            ToPdfMetrics(report.Current),
            ToPdfMetrics(report.Previous));
        var bytes = pdfService.CreateMonthlyReport(model);
        return Results.File(bytes, "application/pdf", $"reporte-{report.From:yyyyMMdd}-{report.To:yyyyMMdd}.pdf");
    }

    private static ReportPdfMetrics ToPdfMetrics(ReportMetrics metrics) => new(
        metrics.GrossSales,
        metrics.ProductRevenue,
        metrics.BilliardRevenue,
        metrics.CostOfGoodsSold,
        metrics.GrossProfit,
        metrics.Expenses,
        metrics.OperatingResult,
        metrics.CashPayments,
        metrics.ElectronicPayments,
        metrics.BilliardHours,
        metrics.CompletedSales,
        metrics.CancelledSales);
}
