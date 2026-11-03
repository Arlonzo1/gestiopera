using BillaresPos.Api.Errors;
using BillaresPos.Application.Common.Abstractions;
using BillaresPos.Application.Common.Exceptions;
using BillaresPos.Domain.CashManagement;
using BillaresPos.Domain.Sales;
using BillaresPos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillaresPos.Api.CashManagement;

public static class CashManagementEndpoints
{
    public static void MapCashManagementEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/cash").RequireAuthorization("CashierOrAbove").WithTags("Cash Management");
        group.MapGet("/current", GetCurrentAsync);
        group.MapGet("/history", GetHistoryAsync);
        group.MapPost("/open", OpenAsync);
        group.MapPost("/current/close", CloseCurrentAsync);
        group.MapPost("/{shiftId:guid}/close", CloseAsync);
        group.MapPost("/expenses", CreateExpenseAsync);
        group.MapGet("/expenses", GetExpensesAsync);
    }

    private static async Task<IResult> GetCurrentAsync(AppDbContext db, ICurrentUser user, CancellationToken ct)
    {
        if (user.UserId is null) throw new ForbiddenException("An authenticated user is required.");
        var shift = await db.CashShifts.AsNoTracking()
            .Where(x => x.UserId == user.UserId && x.Status == CashShiftStatus.Open)
            .OrderByDescending(x => x.OpenedAt).FirstOrDefaultAsync(ct);
        if (shift is null) return Results.Ok((CashShiftResponse?)null);
        return Results.Ok(await SummaryAsync(shift, db, ct));
    }

    private static async Task<IResult> GetHistoryAsync(AppDbContext db, ICurrentUser user, CancellationToken ct)
    {
        if (user.UserId is null) throw new ForbiddenException("Se requiere un usuario autenticado.");
        var allUsers = user.IsInRole(BillaresPos.Domain.Identity.RoleNames.Owner) || user.IsInRole(BillaresPos.Domain.Identity.RoleNames.Administrator);
        var shifts = await db.CashShifts.AsNoTracking()
            .Where(x => x.Status == CashShiftStatus.Closed && (allUsers || x.UserId == user.UserId))
            .OrderByDescending(x => x.ClosedAt ?? x.OpenedAt).Take(50).ToListAsync(ct);
        var result = new List<CashShiftHistoryResponse>(shifts.Count);
        foreach (var shift in shifts)
        {
            var summary = await SummaryAsync(shift, db, ct);
            result.Add(new CashShiftHistoryResponse(shift.Id, shift.UserId, shift.OpenedAt, shift.ClosedAt, shift.OpeningCash, summary.CashSales, summary.CashExpenses, shift.ExpectedCash, shift.CountedCash, shift.Difference, shift.Status));
        }
        return Results.Ok(result);
    }

    private static async Task<IResult> OpenAsync(OpenCashShiftRequest request, AppDbContext db, ICurrentTenant tenant, ICurrentUser user, IClock clock, CancellationToken ct)
    {
        if (tenant.TenantId is null || user.UserId is null || request.OpeningCash < 0)
            throw new ValidationAppException("Datos de apertura de caja no validos.", new Dictionary<string, string[]> { ["openingCash"] = ["El efectivo inicial no puede ser negativo."] });
        if (await db.CashShifts.AnyAsync(x => x.UserId == user.UserId && x.Status == CashShiftStatus.Open, ct))
            throw new ValidationAppException("El usuario ya tiene un turno de caja abierto.", new Dictionary<string, string[]> { ["status"] = ["Cierra el turno actual antes de abrir otro."] });

        var shift = new CashShift { Id = Guid.NewGuid(), TenantId = tenant.TenantId.Value, UserId = user.UserId.Value, OpenedAt = clock.UtcNow, OpeningCash = request.OpeningCash, ExpectedCash = request.OpeningCash };
        db.CashShifts.Add(shift);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/cash/{shift.Id}", ToResponse(shift));
    }

    private static async Task<IResult> CloseCurrentAsync(CloseCashShiftRequest request, AppDbContext db, ICurrentUser user, IClock clock, CancellationToken ct)
    {
        if (user.UserId is null) throw new ForbiddenException("An authenticated user is required.");
        var shiftId = await db.CashShifts.Where(x => x.UserId == user.UserId && x.Status == CashShiftStatus.Open).OrderByDescending(x => x.OpenedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        if (shiftId is null) throw new NotFoundException("No se encontro un turno de caja abierto.");
        return await CloseAsync(shiftId.Value, request, db, user, clock, ct);
    }
    private static async Task<IResult> CloseAsync(Guid shiftId, CloseCashShiftRequest request, AppDbContext db, ICurrentUser user, IClock clock, CancellationToken ct)
    {
        if (user.UserId is null || request.CountedCash < 0)
            throw new ValidationAppException("Datos de cierre de caja no validos.", new Dictionary<string, string[]> { ["countedCash"] = ["El efectivo contado no puede ser negativo."] });
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var shift = await db.CashShifts.FirstOrDefaultAsync(x => x.Id == shiftId && x.UserId == user.UserId && x.Status == CashShiftStatus.Open, ct);
        if (shift is null) throw new NotFoundException("No se encontro un turno de caja abierto.");
        var summary = await SummaryAsync(shift, db, ct);
        shift.ExpectedCash = summary.ExpectedCash;
        shift.CountedCash = request.CountedCash;
        shift.Difference = decimal.Round(request.CountedCash - summary.ExpectedCash, 2);
        shift.ClosedAt = clock.UtcNow;
        shift.Status = CashShiftStatus.Closed;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.Ok(ToResponse(shift));
    }

    private static async Task<IResult> CreateExpenseAsync(CreateExpenseRequest request, AppDbContext db, ICurrentTenant tenant, ICurrentUser user, IClock clock, CancellationToken ct)
    {
        if (tenant.TenantId is null || user.UserId is null || request.Amount <= 0 || string.IsNullOrWhiteSpace(request.Category))
            throw new ValidationAppException("Invalid expense data.", new Dictionary<string, string[]> { ["expense"] = ["Amount must be positive and category is required."] });
        var shift = await db.CashShifts.FirstOrDefaultAsync(x => x.Status == CashShiftStatus.Open && (x.UserId == user.UserId || user.IsInRole(BillaresPos.Domain.Identity.RoleNames.Owner) || user.IsInRole(BillaresPos.Domain.Identity.RoleNames.Administrator)), ct);
        if (shift is null) throw new ValidationAppException("Debes abrir un turno de caja antes de registrar un gasto.", new Dictionary<string, string[]> { ["cashShift"] = ["Abre un turno de caja antes de registrar un gasto."] });
        var expense = new Expense { Id = Guid.NewGuid(), TenantId = tenant.TenantId.Value, CashShiftId = shift.Id, Amount = request.Amount, PaymentMethod = request.PaymentMethod, Category = request.Category.Trim(), Description = request.Description?.Trim(), CreatedByUserId = user.UserId.Value, CreatedAt = clock.UtcNow };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/cash/expenses/{expense.Id}", new ExpenseResponse(expense.Id, expense.Amount, expense.PaymentMethod, expense.Category, expense.Description, expense.CreatedAt));
    }

    private static async Task<IResult> GetExpensesAsync(AppDbContext db, ICurrentUser user, CancellationToken ct)
    {
        if (user.UserId is null) throw new ForbiddenException("An authenticated user is required.");
        var expenses = await db.Expenses.AsNoTracking().Where(x => x.CreatedByUserId == user.UserId).OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct);
        return Results.Ok(expenses.Select(x => new ExpenseResponse(x.Id, x.Amount, x.PaymentMethod, x.Category, x.Description, x.CreatedAt)));
    }

    private static async Task<CashShiftSummaryResponse> SummaryAsync(CashShift shift, AppDbContext db, CancellationToken ct)
    {
        var cashSales = await db.SalePayments.Where(x => x.Sale.CashShiftId == shift.Id && x.Sale.Status == SaleStatus.Completed && x.Method == PaymentMethod.Cash).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        var cashExpenses = await db.Expenses.Where(x => x.CashShiftId == shift.Id && x.PaymentMethod == ExpensePaymentMethod.Cash).SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        var expected = decimal.Round(shift.OpeningCash + cashSales - cashExpenses, 2);
        return new CashShiftSummaryResponse(shift.Id, shift.OpeningCash, cashSales, cashExpenses, expected, shift.Status);
    }

    private static CashShiftResponse ToResponse(CashShift shift) => new(shift.Id, shift.UserId, shift.OpenedAt, shift.ClosedAt, shift.OpeningCash, shift.ExpectedCash, shift.CountedCash, shift.Difference, shift.Status);
}


