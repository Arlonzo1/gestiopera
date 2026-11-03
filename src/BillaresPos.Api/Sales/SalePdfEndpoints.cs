using BillaresPos.Api.Billiards;
using BillaresPos.Application.Common.Abstractions;
using BillaresPos.Application.Common.Exceptions;
using BillaresPos.Infrastructure.Identity;
using BillaresPos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillaresPos.Api.Sales;

public static class SalePdfEndpoints
{
    public static void MapSalePdfEndpoints(this WebApplication app)
    {
        app.MapGet("/api/sales/{id:guid}/receipt", GetReceiptAsync)
            .RequireAuthorization("CashierOrAbove")
            .WithTags("Sales")
            .Produces(200, contentType: "application/pdf");
    }

    private static async Task<IResult> GetReceiptAsync(
        Guid id,
        AppDbContext db,
        IPdfService pdfService,
        CancellationToken ct)
    {
        var sale = await db.Sales.AsNoTracking()
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (sale is null)
            throw new NotFoundException("Venta no encontrada.");

        var tenant = await db.Tenants.AsNoTracking().SingleAsync(x => x.Id == sale.TenantId, ct);
        var cashier = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sale.UserId, ct);
        var timeZone = FindTimeZone(tenant.TimeZoneId);
        var localCreatedAt = TimeZoneInfo.ConvertTime(sale.CreatedAt, timeZone);
        var billiard = await BilliardSaleLookup.FindAsync(db, sale, ct);
        var model = new SalesReceiptPdfModel(
            tenant.Name,
            tenant.Currency,
            sale.SequentialNumber,
            localCreatedAt,
            cashier?.FullName ?? "Usuario",
            sale.CustomerName,
            sale.Items.OrderBy(x => x.Id).Select(x => new SalesReceiptPdfItem(
                x.DescriptionSnapshot,
                x.Quantity,
                x.UnitPrice,
                x.Subtotal,
                x.ItemType == Domain.Sales.SaleItemType.BilliardTime)).ToList(),
            sale.Subtotal,
            sale.Discount,
            sale.Total,
            sale.Payments.Select(x => new SalesReceiptPdfPayment(PaymentName(x.Method), x.Amount)).ToList(),
            billiard?.TableName,
            billiard is null ? null : TimeZoneInfo.ConvertTime(billiard.StartedAtUtc, timeZone),
            billiard?.EndedAtUtc is null ? null : TimeZoneInfo.ConvertTime(billiard.EndedAtUtc.Value, timeZone));

        var bytes = pdfService.CreateSalesReceipt(model);
        return Results.File(bytes, "application/pdf", $"comprobante-venta-{sale.SequentialNumber}.pdf");
    }

    private static string PaymentName(Domain.Sales.PaymentMethod method) => method switch
    {
        Domain.Sales.PaymentMethod.Cash => "Efectivo",
        Domain.Sales.PaymentMethod.Nequi => "Nequi",
        Domain.Sales.PaymentMethod.Daviplata => "Daviplata",
        Domain.Sales.PaymentMethod.Card => "Tarjeta",
        Domain.Sales.PaymentMethod.BankTransfer => "Transferencia bancaria",
        _ => "Otro"
    };

    private static TimeZoneInfo FindTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) when (id == "America/Bogota")
        { return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time"); }
    }
}
