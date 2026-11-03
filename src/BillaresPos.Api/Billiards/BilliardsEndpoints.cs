using BillaresPos.Api.Auth;
using BillaresPos.Api.Errors;
using BillaresPos.Application.Common.Abstractions;
using BillaresPos.Application.Common.Exceptions;
using BillaresPos.Domain.Billiards;
using BillaresPos.Domain.Inventory;
using BillaresPos.Domain.Sales;
using BillaresPos.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BillaresPos.Api.Billiards;

public static class BilliardsEndpoints
{
    public static void MapBilliardsEndpoints(this WebApplication app)
    {
        app.MapHub<BilliardsHub>("/hubs/billiards");

        var group = app.MapGroup("/api/billiards")
            .RequireAuthorization("CashierOrAbove")
            .WithTags("Billiards");

        group.MapGet("/tables", GetTablesAsync);
        group.MapGet("/sessions/{sessionId:guid}", GetSessionAsync);
        group.MapPost("/tables", CreateTableAsync).RequireAuthorization("AdministratorOrOwner");
        group.MapPatch("/tables/{tableId:guid}", UpdateTableAsync).RequireAuthorization("AdministratorOrOwner");
        group.MapPost("/tables/{tableId:guid}/start", StartAsync);
        group.MapPost("/tables/{tableId:guid}/pause", PauseAsync);
        group.MapPost("/tables/{tableId:guid}/resume", ResumeAsync);
        group.MapPost("/sessions/{sessionId:guid}/consumptions", AddConsumptionAsync);
        group.MapDelete("/sessions/{sessionId:guid}/consumptions/{consumptionId:guid}", DeleteConsumptionAsync);
        group.MapPost("/sessions/{sessionId:guid}/close", CloseAsync);
    }

    private static async Task<IResult> GetTablesAsync(AppDbContext db, CancellationToken ct)
    {
        var active = await db.BilliardSessions
            .Where(x => x.Status == BilliardSessionStatus.Active)
            .ToDictionaryAsync(x => x.BilliardTableId, ct);
        var tables = await db.BilliardTables.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Name).ToListAsync(ct);

        return Results.Ok(tables.Select(table =>
        {
            active.TryGetValue(table.Id, out var session);
            return new BilliardTableResponse(table.Id, table.Name, table.HourlyRate,
                table.BillingIncrementMinutes, table.Status, session?.Id,
                session?.StartedAtUtc, session?.TotalPausedSeconds ?? 0, session?.PausedAtUtc);
        }));
    }

    private static async Task<IResult> CreateTableAsync(
        CreateBilliardTableRequest request, AppDbContext db, ICurrentTenant tenant,
        CancellationToken ct)
    {
        if (tenant.TenantId is null || string.IsNullOrWhiteSpace(request.Name) ||
            request.HourlyRate < 0 || request.BillingIncrementMinutes <= 0)
        {
            throw new ValidationAppException("Datos de mesa de billar no validos.",
                new Dictionary<string, string[]> { ["table"] = ["El nombre es obligatorio. La tarifa no puede ser negativa y el incremento debe ser positivo."] });
        }
        var name = request.Name.Trim();
        if (await db.BilliardTables.AnyAsync(x => x.Name == name, ct))
        {
            throw new ValidationAppException("Ya existe una mesa con el mismo nombre.",
                new Dictionary<string, string[]> { ["name"] = ["El nombre de la mesa debe ser unico dentro del negocio."] });
        }
        var table = new BilliardTable
        {
            Id = Guid.NewGuid(), TenantId = tenant.TenantId.Value, Name = name,
            HourlyRate = request.HourlyRate,
            BillingIncrementMinutes = request.BillingIncrementMinutes
        };
        db.BilliardTables.Add(table);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/billiards/tables/{table.Id}",
            new BilliardTableResponse(table.Id, table.Name, table.HourlyRate,
                table.BillingIncrementMinutes, table.Status, null, null, 0, null));
    }

    private static async Task<IResult> UpdateTableAsync(
        Guid tableId, UpdateBilliardTableRequest request, AppDbContext db,
        ICurrentTenant tenant, CancellationToken ct)
    {
        if (tenant.TenantId is null || string.IsNullOrWhiteSpace(request.Name) ||
            request.HourlyRate < 0 || decimal.Truncate(request.HourlyRate) != request.HourlyRate ||
            request.BillingIncrementMinutes <= 0)
        {
            throw new ValidationAppException("Datos de mesa de billar no validos.",
                new Dictionary<string, string[]> { ["table"] = ["El nombre es obligatorio. La tarifa debe ser un numero entero no negativo y el incremento debe ser positivo."] });
        }

        var table = await db.BilliardTables.FirstOrDefaultAsync(x => x.Id == tableId && x.IsActive, ct);
        if (table is null) throw new NotFoundException("No se encontro la mesa de billar.");
        var name = request.Name.Trim();
        if (await db.BilliardTables.AnyAsync(x => x.Id != tableId && x.Name == name, ct))
        {
            throw new ValidationAppException("Ya existe una mesa con el mismo nombre.",
                new Dictionary<string, string[]> { ["name"] = ["El nombre de la mesa debe ser unico dentro del negocio."] });
        }

        table.Name = name;
        table.HourlyRate = request.HourlyRate;
        table.BillingIncrementMinutes = request.BillingIncrementMinutes;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new BilliardTableResponse(table.Id, table.Name, table.HourlyRate,
            table.BillingIncrementMinutes, table.Status, null, null, 0, null));
    }
    private static async Task<IResult> StartAsync(
        Guid tableId, AppDbContext db, ICurrentTenant tenant, ICurrentUser user,
        IClock clock, IHubContext<BilliardsHub> hub, CancellationToken ct)
    {
        if (tenant.TenantId is null || user.UserId is null)
            throw new ForbiddenException("An authenticated tenant user is required.");
        if (!await db.CashShifts.AnyAsync(x => x.UserId == user.UserId && x.Status == BillaresPos.Domain.CashManagement.CashShiftStatus.Open, ct))
            throw new ValidationAppException("Debes abrir un turno de caja antes de iniciar una mesa.",
                new Dictionary<string, string[]> { ["cashShift"] = ["Abre un turno de caja para iniciar la operacion."] });

        await using var tx = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);
        var table = await db.BilliardTables.FirstOrDefaultAsync(
            x => x.Id == tableId && x.IsActive, ct);
        if (table is null) throw new NotFoundException("No se encontro la mesa de billar.");
        if (table.Status != BilliardTableStatus.Available)
            throw new ValidationAppException("La mesa no esta disponible.",
                new Dictionary<string, string[]> { ["status"] = ["Solo se pueden iniciar mesas disponibles."] });

        var session = new BilliardSession
        {
            Id = Guid.NewGuid(), TenantId = tenant.TenantId.Value,
            BilliardTableId = table.Id, StartedAtUtc = clock.UtcNow,
            HourlyRateSnapshot = table.HourlyRate,
            BillingIncrementMinutesSnapshot = table.BillingIncrementMinutes,
            OpenedByUserId = user.UserId.Value
        };
        table.Status = BilliardTableStatus.Occupied;
        db.BilliardSessions.Add(session);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await BroadcastAsync(hub, tenant.TenantId.Value, table, session, ct);
        return Results.Created($"/api/billiards/sessions/{session.Id}",
            new BilliardSessionResponse(session.Id, table.Id, session.Status, 0, 0, 0, 0, 0, null));
    }

    private static async Task<IResult> PauseAsync(
        Guid tableId, AppDbContext db, ICurrentTenant tenant, IClock clock,
        IHubContext<BilliardsHub> hub, CancellationToken ct)
    {
        var (table, session) = await ActiveTableAsync(tableId, db, ct);
        if (table.Status != BilliardTableStatus.Occupied || session.PausedAtUtc is not null)
            throw new ValidationAppException("La sesion no se puede pausar.", new Dictionary<string, string[]>());
        session.PausedAtUtc = clock.UtcNow;
        table.Status = BilliardTableStatus.Paused;
        await db.SaveChangesAsync(ct);
        await BroadcastAsync(hub, tenant.TenantId!.Value, table, session, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ResumeAsync(
        Guid tableId, AppDbContext db, ICurrentTenant tenant, IClock clock,
        IHubContext<BilliardsHub> hub, CancellationToken ct)
    {
        var (table, session) = await ActiveTableAsync(tableId, db, ct);
        if (table.Status != BilliardTableStatus.Paused || session.PausedAtUtc is null)
            throw new ValidationAppException("La sesion no se puede reanudar.", new Dictionary<string, string[]>());
        session.TotalPausedSeconds += Math.Max(0,
            (long)(clock.UtcNow - session.PausedAtUtc.Value).TotalSeconds);
        session.PausedAtUtc = null;
        table.Status = BilliardTableStatus.Occupied;
        await db.SaveChangesAsync(ct);
        await BroadcastAsync(hub, tenant.TenantId!.Value, table, session, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> AddConsumptionAsync(
        Guid sessionId, AddBilliardConsumptionRequest request, AppDbContext db,
        ICurrentTenant tenant, ICurrentUser user, IClock clock, CancellationToken ct)
    {
        if (tenant.TenantId is null || user.UserId is null || request.Quantity <= 0 || decimal.Truncate(request.Quantity) != request.Quantity)
            throw new ValidationAppException("Consumo de billar no valido.",
                new Dictionary<string, string[]> { ["quantity"] = ["La cantidad debe ser positiva."] });
        var session = await db.BilliardSessions.FirstOrDefaultAsync(
            x => x.Id == sessionId && x.Status == BilliardSessionStatus.Active, ct);
        if (session is null) throw new NotFoundException("No se encontro la sesion activa de billar.");
        var product = await db.Products.FirstOrDefaultAsync(
            x => x.Id == request.ProductId && x.IsActive, ct);
        if (product is null) throw new NotFoundException("No se encontro el producto.");

        var consumption = new BilliardConsumption
        {
            Id = Guid.NewGuid(), TenantId = tenant.TenantId.Value,
            BilliardSessionId = session.Id, ProductId = product.Id,
            DescriptionSnapshot = product.Name, Quantity = request.Quantity,
            UnitPrice = product.SalePrice, UnitCostAtConsumption = product.AverageCost,
            Subtotal = decimal.Round(request.Quantity * product.SalePrice, 2),
            AddedByUserId = user.UserId.Value, CreatedAt = clock.UtcNow
        };
        db.BilliardConsumptions.Add(consumption);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/billiards/sessions/{session.Id}/consumptions/{consumption.Id}", consumption.Id);
    }

    private static async Task<IResult> DeleteConsumptionAsync(
        Guid sessionId, Guid consumptionId, AppDbContext db, ICurrentTenant tenant, CancellationToken ct)
    {
        if (tenant.TenantId is null)
            throw new ForbiddenException("An authenticated tenant user is required.");

        var consumption = await db.BilliardConsumptions
            .FirstOrDefaultAsync(x => x.Id == consumptionId && x.BilliardSessionId == sessionId && x.TenantId == tenant.TenantId && x.BilliardSession.Status == BilliardSessionStatus.Active, ct);
        if (consumption is null)
            throw new NotFoundException("No se encontro el consumo en la sesion activa.");

        db.BilliardConsumptions.Remove(consumption);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
    private static async Task<IResult> CloseAsync(
        Guid sessionId, CloseBilliardSessionRequest request, AppDbContext db,
        ICurrentTenant tenant, ICurrentUser user, IClock clock,
        IHubContext<BilliardsHub> hub, CancellationToken ct)
    {
        if (tenant.TenantId is null || user.UserId is null || request.Payments is null ||
            request.Payments.Count == 0 || request.Payments.Any(x => x.Amount <= 0 || decimal.Truncate(x.Amount) != x.Amount))
            throw new ValidationAppException("Datos de cierre de billar no validos.",
                new Dictionary<string, string[]> { ["payments"] = ["Debes registrar al menos un pago con valor positivo."] });

        await using var tx = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);
        var session = await db.BilliardSessions.Include(x => x.Consumptions)
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.Status == BilliardSessionStatus.Active, ct);
        if (session is null) throw new NotFoundException("No se encontro la sesion activa de billar.");
        var table = await db.BilliardTables.FirstOrDefaultAsync(x => x.Id == session.BilliardTableId, ct);
        if (table is null) throw new NotFoundException("No se encontro la mesa de billar.");

        var ended = clock.UtcNow;
        var pausedSeconds = session.TotalPausedSeconds + (session.PausedAtUtc is null
            ? 0
            : Math.Max(0, (long)(ended - session.PausedAtUtc.Value).TotalSeconds));
        var elapsed = Math.Max(0, (long)(ended - session.StartedAtUtc).TotalSeconds - pausedSeconds);
        var rawMinutes = elapsed / 60m;
        var billed = rawMinutes == 0 ? 0 : (int)(Math.Ceiling(rawMinutes / session.BillingIncrementMinutesSnapshot) * session.BillingIncrementMinutesSnapshot);
        var timeCharge = decimal.Round(billed * session.HourlyRateSnapshot / 60m, 2, MidpointRounding.ToEven);
        var consumptionTotal = decimal.Round(session.Consumptions.Sum(x => x.Subtotal), 2);
        var total = decimal.Round(timeCharge + consumptionTotal, 2);
        if (decimal.Round(request.Payments.Sum(x => x.Amount), 2) != total)
            throw new ValidationAppException("El total recibido no coincide con el total de la sesion de billar.",
                new Dictionary<string, string[]> { ["payments"] = ["Los valores de los pagos deben ser iguales al total de la sesion."] });

        var productIds = session.Consumptions.Select(x => x.ProductId).Distinct().ToArray();
        var products = await db.Products.Where(x => productIds.Contains(x.Id) && x.IsActive)
            .ToDictionaryAsync(x => x.Id, ct);
        var quantities = session.Consumptions.GroupBy(x => x.ProductId)
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Quantity));
        if (products.Count != productIds.Length)
            throw new NotFoundException("Uno de los productos consumidos ya no existe.");
        foreach (var pair in quantities)
            if (products[pair.Key].CurrentStock < pair.Value)
                throw new ValidationAppException("Inventario insuficiente para el consumo de billar.",
                    new Dictionary<string, string[]> { ["quantity"] = [$"El producto {pair.Key} no tiene inventario suficiente."] });

        var number = (await db.Sales.Select(x => (long?)x.SequentialNumber).MaxAsync(ct) ?? 0) + 1;
        var activeCashShiftId = await db.CashShifts
            .Where(x => x.UserId == user.UserId && x.Status == BillaresPos.Domain.CashManagement.CashShiftStatus.Open)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);
        var sale = new Sale
        {
            Id = Guid.NewGuid(), TenantId = session.TenantId, SequentialNumber = number,
            UserId = user.UserId.Value, CreatedAt = ended, Status = SaleStatus.Completed,
            CashShiftId = activeCashShiftId, Subtotal = total, Total = total,
            CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? null : request.CustomerName.Trim(),
            Notes = $"Billiard session {session.Id}"
        };
        if (billed > 0)
            sale.Items.Add(new SaleItem
            {
                Id = Guid.NewGuid(), TenantId = sale.TenantId, SaleId = sale.Id,
                DescriptionSnapshot = $"Billiard time - {table.Name} ({billed} minutes)",
                Quantity = billed, UnitPrice = decimal.Round(session.HourlyRateSnapshot / 60m, 2),
                Subtotal = timeCharge, ItemType = SaleItemType.BilliardTime
            });
        foreach (var item in session.Consumptions)
            sale.Items.Add(new SaleItem
            {
                Id = Guid.NewGuid(), TenantId = sale.TenantId, SaleId = sale.Id,
                ProductId = item.ProductId, DescriptionSnapshot = item.DescriptionSnapshot,
                Quantity = item.Quantity, UnitPrice = item.UnitPrice,
                UnitCostAtSale = item.UnitCostAtConsumption, Subtotal = item.Subtotal,
                ItemType = SaleItemType.Product
            });
        foreach (var payment in request.Payments)
            sale.Payments.Add(new SalePayment
            {
                Id = Guid.NewGuid(), TenantId = sale.TenantId, SaleId = sale.Id,
                Method = payment.Method, Amount = payment.Amount
            });
        foreach (var pair in quantities)
        {
            products[pair.Key].CurrentStock -= pair.Value;
            db.InventoryMovements.Add(new InventoryMovement
            {
                Id = Guid.NewGuid(), TenantId = sale.TenantId, ProductId = pair.Key,
                MovementType = InventoryMovementType.Sale, Quantity = -pair.Value,
                UnitCost = products[pair.Key].AverageCost, ReferenceType = "BilliardSession",
                ReferenceId = session.Id, CreatedByUserId = user.UserId.Value, CreatedAt = ended
            });
        }
        session.EndedAtUtc = ended;
        session.TotalPausedSeconds = pausedSeconds;
        session.PausedAtUtc = null;
        session.ClosedByUserId = user.UserId.Value;
        session.Status = BilliardSessionStatus.Closed;
        table.Status = BilliardTableStatus.Available;
        db.Sales.Add(sale);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await BroadcastAsync(hub, tenant.TenantId.Value, table, session, ct);
        return Results.Ok(new BilliardSessionResponse(session.Id, table.Id, session.Status,
            elapsed, billed, timeCharge, consumptionTotal, total, sale.Id));
    }

    private static async Task<IResult> GetSessionAsync(Guid sessionId, AppDbContext db, IClock clock, CancellationToken ct)
    {
        var session = await db.BilliardSessions.Include(x => x.Consumptions).FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (session is null) throw new NotFoundException("No se encontro la sesion de billar.");
        var table = await db.BilliardTables.AsNoTracking().FirstOrDefaultAsync(x => x.Id == session.BilliardTableId, ct);
        if (table is null) throw new NotFoundException("No se encontro la mesa de billar.");
        var now = session.EndedAtUtc ?? clock.UtcNow;
        var paused = session.TotalPausedSeconds + (session.PausedAtUtc is null ? 0 : Math.Max(0, (long)(now - session.PausedAtUtc.Value).TotalSeconds));
        var elapsed = Math.Max(0, (long)(now - session.StartedAtUtc).TotalSeconds - paused);
        var rawMinutes = elapsed / 60m;
        var billed = rawMinutes == 0 ? 0 : (int)(Math.Ceiling(rawMinutes / session.BillingIncrementMinutesSnapshot) * session.BillingIncrementMinutesSnapshot);
        var timeCharge = decimal.Round(billed * session.HourlyRateSnapshot / 60m, 2, MidpointRounding.ToEven);
        var consumptionTotal = decimal.Round(session.Consumptions.Sum(x => x.Subtotal), 2);
        return Results.Ok(new BilliardSessionDetailResponse(session.Id, table.Id, session.Status, elapsed, billed, timeCharge, consumptionTotal, decimal.Round(timeCharge + consumptionTotal, 2), session.Consumptions.OrderBy(x => x.CreatedAt).Select(x => new BilliardConsumptionResponse(x.Id, x.ProductId, x.DescriptionSnapshot, x.Quantity, x.UnitPrice, x.Subtotal)).ToList()));
    }

    private static async Task<(BilliardTable Table, BilliardSession Session)> ActiveTableAsync(
        Guid tableId, AppDbContext db, CancellationToken ct)
    {
        var table = await db.BilliardTables.FirstOrDefaultAsync(x => x.Id == tableId, ct);
        var session = table is null ? null : await db.BilliardSessions.FirstOrDefaultAsync(
            x => x.BilliardTableId == table.Id && x.Status == BilliardSessionStatus.Active, ct);
        if (table is null || session is null) throw new NotFoundException("No se encontro la sesion activa de billar.");
        return (table, session);
    }

    private static Task BroadcastAsync(IHubContext<BilliardsHub> hub, Guid tenantId,
        BilliardTable table, BilliardSession session, CancellationToken ct) =>
        hub.Clients.Group($"tenant:{tenantId}").SendAsync("tableUpdated", new
        {
            TableId = table.Id, TableName = table.Name, Status = table.Status,
            SessionId = session.Id, session.StartedAtUtc, session.TotalPausedSeconds,
            SessionStatus = session.Status
        }, ct);
}






