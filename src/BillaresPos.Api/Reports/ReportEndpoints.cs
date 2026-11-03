using BillaresPos.Application.Common.Abstractions;
using BillaresPos.Application.Common.Exceptions;
using BillaresPos.Domain.Billiards;
using BillaresPos.Domain.CashManagement;
using BillaresPos.Domain.Sales;
using BillaresPos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillaresPos.Api.Reports;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this WebApplication app)
    {
        app.MapGet("/api/reports", GetAsync)
            .RequireAuthorization("AdministratorOrOwner")
            .WithTags("Reports");
    }

    public static async Task<IResult> GetAsync(
        DateOnly? from,
        DateOnly? to,
        AppDbContext db,
        ICurrentTenant currentTenant,
        IClock clock,
        CancellationToken ct)
    {
        if (currentTenant.TenantId is null)
            return Results.Unauthorized();

        var tenant = await db.Tenants.AsNoTracking()
            .SingleAsync(x => x.Id == currentTenant.TenantId.Value, ct);
        var timeZone = FindTimeZone(tenant.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(clock.UtcNow, timeZone);
        var currentTo = to ?? DateOnly.FromDateTime(localNow.DateTime);
        var currentFrom = from ?? currentTo;

        if (currentFrom > currentTo)
            throw new ValidationAppException("El rango de fechas no es valido.", new Dictionary<string, string[]> { ["from"] = ["La fecha inicial no puede ser posterior a la fecha final."] });
        if (currentTo.DayNumber - currentFrom.DayNumber > 366)
            throw new ValidationAppException("El rango de fechas no puede superar 367 dias.", new Dictionary<string, string[]> { ["to"] = ["Reduce el rango de fechas."] });

        var days = currentTo.DayNumber - currentFrom.DayNumber + 1;
        var previousTo = currentFrom.AddDays(-1);
        var previousFrom = previousTo.AddDays(-(days - 1));

        var current = await BuildMetricsAsync(db, currentTenant.TenantId.Value, currentFrom, currentTo, timeZone, ct);
        var previous = await BuildMetricsAsync(db, currentTenant.TenantId.Value, previousFrom, previousTo, timeZone, ct);

        return Results.Ok(new ReportResponse(currentFrom, currentTo, previousFrom, previousTo, current, previous));
    }

    public static async Task<ReportMetrics> BuildMetricsAsync(AppDbContext db, Guid tenantId, DateOnly from, DateOnly to, TimeZoneInfo timeZone, CancellationToken ct)
    {
        var (utcStart, utcEnd) = UtcRange(from, to, timeZone);
        var allSales = await db.Sales.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).AsNoTracking().ToListAsync(ct);
        var sales = allSales
            .Where(x => x.CreatedAt >= utcStart && x.CreatedAt < utcEnd && x.Status == SaleStatus.Completed)
            .ToList();
        var saleIds = sales.Select(x => x.Id).ToArray();
        var items = await db.SaleItems.IgnoreQueryFilters().Where(x => x.TenantId == tenantId && saleIds.Contains(x.SaleId)).AsNoTracking().ToListAsync(ct);
        var payments = await db.SalePayments.IgnoreQueryFilters().Where(x => x.TenantId == tenantId && saleIds.Contains(x.SaleId)).AsNoTracking().ToListAsync(ct);
        var expenses = (await db.Expenses.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).AsNoTracking().ToListAsync(ct))
            .Where(x => x.CreatedAt >= utcStart && x.CreatedAt < utcEnd)
            .ToList();

        var grossSales = sales.Sum(x => x.Total);
        var productRevenue = items.Where(x => x.ItemType == SaleItemType.Product).Sum(x => x.Subtotal);
        var billiardRevenue = items.Where(x => x.ItemType == SaleItemType.BilliardTime).Sum(x => x.Subtotal);
        var cogs = items.Sum(x => x.UnitCostAtSale * x.Quantity);
        var totalExpenses = expenses.Sum(x => x.Amount);
        var cashPayments = payments.Where(x => x.Method == PaymentMethod.Cash).Sum(x => x.Amount);
        var electronicPayments = payments.Where(x => x.Method != PaymentMethod.Cash).Sum(x => x.Amount);

        var paymentRows = payments
            .GroupBy(x => x.Method)
            .Select(x => new ReportPaymentRow(x.Key, x.Sum(payment => payment.Amount)))
            .OrderByDescending(x => x.Amount)
            .ToList();

        var salesByDay = sales
            .GroupBy(x => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(x.CreatedAt, timeZone).DateTime))
            .Select(x => new ReportDailyRow(x.Key, x.Sum(row => row.Total)))
            .OrderBy(x => x.Date)
            .ToList();

        var employeeRows = sales
            .GroupBy(x => x.UserId)
            .Select(x => new { UserId = x.Key, Amount = x.Sum(sale => sale.Total), SalesCount = x.Count() })
            .OrderByDescending(x => x.Amount)
            .ToList();
        var employeeIds = employeeRows.Select(x => x.UserId).ToArray();
        var employeeNames = await db.Users.IgnoreQueryFilters()
            .Where(x => employeeIds.Contains(x.Id) && x.TenantId == tenantId)
            .Select(x => new { x.Id, x.FullName })
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var salesByEmployee = employeeRows
            .Select(x => new ReportEmployeeRow(x.UserId, employeeNames.GetValueOrDefault(x.UserId, "Usuario"), x.Amount, x.SalesCount))
            .ToList();

        var productRows = items
            .GroupBy(x => new { x.ProductId, x.DescriptionSnapshot })
            .Select(x => new ReportProductRow(
                x.Key.ProductId,
                x.Key.DescriptionSnapshot,
                x.Sum(item => item.Quantity),
                x.Sum(item => item.Subtotal),
                x.Sum(item => item.UnitCostAtSale * item.Quantity),
                x.Sum(item => (item.UnitPrice - item.UnitCostAtSale) * item.Quantity)))
            .OrderByDescending(x => x.Revenue)
            .Take(20)
            .ToList();

        var allSessions = await db.BilliardSessions.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).AsNoTracking().ToListAsync(ct);
        var billiardSessions = allSessions
            .Where(x => x.StartedAtUtc < utcEnd && (x.EndedAtUtc ?? utcEnd) >= utcStart)
            .ToList();
        var billiardHours = billiardSessions.Sum(x =>
        {
            var start = x.StartedAtUtc < utcStart ? utcStart : x.StartedAtUtc;
            var end = x.EndedAtUtc is null || x.EndedAtUtc > utcEnd ? utcEnd : x.EndedAtUtc.Value;
            return (decimal)Math.Max(0, (end - start).TotalSeconds - x.TotalPausedSeconds) / 3600m;
        });

        var cancelledSales = allSales.Count(x => x.CreatedAt >= utcStart && x.CreatedAt < utcEnd && x.Status == SaleStatus.Cancelled);
        return new ReportMetrics(
            Round(grossSales), Round(productRevenue), Round(billiardRevenue), Round(cogs),
            Round(grossSales - cogs), Round(totalExpenses), Round(grossSales - cogs - totalExpenses),
            Round(cashPayments), Round(electronicPayments), decimal.Round(billiardHours, 2),
            sales.Count, cancelledSales, paymentRows, salesByDay, salesByEmployee, productRows);
    }
    private static (DateTimeOffset Start, DateTimeOffset End) UtcRange(DateOnly from, DateOnly to, TimeZoneInfo timeZone)
    {
        var localStart = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEnd = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return (new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone)),
            new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone)));
    }

    private static decimal Round(decimal value) => decimal.Round(value, 2);

    private static TimeZoneInfo FindTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) when (id == "America/Bogota")
        { return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time"); }
    }
}