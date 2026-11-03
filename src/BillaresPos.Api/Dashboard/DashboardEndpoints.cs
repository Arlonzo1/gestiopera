using BillaresPos.Application.Common.Abstractions;
using BillaresPos.Domain.CashManagement;
using BillaresPos.Domain.Sales;
using BillaresPos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillaresPos.Api.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        app.MapGet("/api/dashboard", GetAsync)
            .RequireAuthorization("AdministratorOrOwner")
            .WithTags("Dashboard");
    }

    private static async Task<IResult> GetAsync(
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
        var businessDate = DateOnly.FromDateTime(localNow.DateTime);
        var localStart = businessDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var nextLocalStart = localStart.AddDays(1);
        var utcStart = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone));
        var utcEnd = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(nextLocalStart, timeZone));

        var sales = db.Sales.AsNoTracking()
            .Where(x => x.CreatedAt >= utcStart && x.CreatedAt < utcEnd && x.Status == SaleStatus.Completed);
        var grossSales = await sales.SumAsync(x => (decimal?)x.Total, ct) ?? 0;

        var saleItems = db.SaleItems.AsNoTracking()
            .Where(x => x.Sale.CreatedAt >= utcStart && x.Sale.CreatedAt < utcEnd && x.Sale.Status == SaleStatus.Completed);
        var productRevenue = await saleItems.Where(x => x.ItemType == SaleItemType.Product).SumAsync(x => (decimal?)x.Subtotal, ct) ?? 0;
        var billiardRevenue = await saleItems.Where(x => x.ItemType == SaleItemType.BilliardTime).SumAsync(x => (decimal?)x.Subtotal, ct) ?? 0;
        var expenses = await db.Expenses.AsNoTracking()
            .Where(x => x.CreatedAt >= utcStart && x.CreatedAt < utcEnd)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0;

        var paymentRows = await db.SalePayments.AsNoTracking()
            .Where(x => x.Sale.CreatedAt >= utcStart && x.Sale.CreatedAt < utcEnd && x.Sale.Status == SaleStatus.Completed)
            .GroupBy(x => x.Method)
            .Select(x => new { Method = x.Key, Amount = x.Sum(payment => payment.Amount) })
            .ToListAsync(ct);
        var paymentBreakdown = paymentRows
            .Select(x => new DashboardPaymentBreakdown(x.Method, x.Amount))
            .OrderByDescending(x => x.Amount)
            .ToList();

        var totalTables = await db.BilliardTables.CountAsync(x => x.IsActive, ct);
        var activeTables = await db.BilliardSessions.CountAsync(x => x.Status == Domain.Billiards.BilliardSessionStatus.Active, ct);
        var lowStockItems = await db.Products.AsNoTracking()
            .Where(x => x.IsActive && x.CurrentStock <= x.MinimumStock)
            .OrderBy(x => x.CurrentStock)
            .Take(8)
            .Select(x => new DashboardLowStockProduct(x.Id, x.Name, x.CurrentStock, x.MinimumStock, x.CriticalStock))
            .ToListAsync(ct);

        var openCash = await db.CashShifts.AsNoTracking()
            .Where(x => x.Status == CashShiftStatus.Open)
            .ToListAsync(ct);
        decimal expectedCash = 0;
        foreach (var shift in openCash)
        {
            var cashSales = await db.SalePayments.AsNoTracking()
                .Where(x => x.Sale.CashShiftId == shift.Id && x.Sale.Status == SaleStatus.Completed && x.Method == PaymentMethod.Cash)
                .SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
            var cashExpenses = await db.Expenses.AsNoTracking()
                .Where(x => x.CashShiftId == shift.Id && x.PaymentMethod == ExpensePaymentMethod.Cash)
                .SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
            expectedCash += shift.OpeningCash + cashSales - cashExpenses;
        }

        var recentSaleEntities = await sales.Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAt).Take(8).ToListAsync(ct);
        var recentSales = recentSaleEntities.Select(x =>
        {
            var billiardItem = x.Items.FirstOrDefault(item => item.ItemType == SaleItemType.BilliardTime);
            var tableName = billiardItem?.DescriptionSnapshot.Replace("Tiempo de billar - ", string.Empty).Replace("Billiard time - ", string.Empty).Split(" (")[0];
            return new DashboardRecentSale(x.Id, x.SequentialNumber, x.CreatedAt, x.Total, x.Status, x.UserId,
                tableName, billiardItem is null ? null : (int)billiardItem.Quantity, billiardItem?.Subtotal);
        }).ToList();

        return Results.Ok(new DashboardResponse(
            businessDate,
            decimal.Round(grossSales, 2),
            decimal.Round(productRevenue, 2),
            decimal.Round(billiardRevenue, 2),
            decimal.Round(expenses, 2),
            decimal.Round(expectedCash, 2),
            activeTables,
            totalTables,
            lowStockItems.Count,
            paymentBreakdown,
            recentSales,
            lowStockItems));
    }

    private static TimeZoneInfo FindTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) when (id == "America/Bogota")
        { return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time"); }
    }
}
