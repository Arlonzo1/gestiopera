using BillaresPos.Api.Notifications;
using BillaresPos.Application.Common;
using BillaresPos.Application.Common.Abstractions;
using BillaresPos.Domain.Notifications;
using BillaresPos.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace BillaresPos.Api.Jobs;

public sealed class InventoryAlertJobs(
    AppDbContext dbContext,
    NotificationService notificationService,
    IClock clock,
    IBackgroundJobClient backgroundJobs)
{
    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var tenants = await dbContext.Tenants.AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
        var pendingDeliveryIds = new List<Guid>();

        foreach (var tenant in tenants)
        {
            var products = await dbContext.Products.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.TenantId == tenant.Id && x.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var product in products)
            {
                var type = GetAlertType(product.CurrentStock, product.MinimumStock, product.CriticalStock);
                if (type is null)
                {
                    var activeAlerts = await dbContext.Notifications.IgnoreQueryFilters()
                        .Where(x => x.TenantId == tenant.Id && x.ReferenceType == "Product" && x.ReferenceId == product.Id
                            && x.Channel == NotificationChannel.InApp
                            && (x.Type == NotificationType.StockLow || x.Type == NotificationType.StockCritical || x.Type == NotificationType.OutOfStock)
                            && x.ReadAt == null)
                        .ToListAsync(cancellationToken);
                    foreach (var alert in activeAlerts) alert.ReadAt = now;
                    continue;
                }

                var lastAlert = await dbContext.Notifications.IgnoreQueryFilters()
                    .Where(x => x.TenantId == tenant.Id && x.ReferenceType == "Product" && x.ReferenceId == product.Id &&
                        x.Status != NotificationStatus.Failed)
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => new { x.Type, x.CreatedAt })
                    .FirstOrDefaultAsync(cancellationToken);
                var replenishedAfterAlert = lastAlert is not null &&
                    await dbContext.InventoryMovements.IgnoreQueryFilters().AnyAsync(x =>
                        x.TenantId == tenant.Id && x.ProductId == product.Id && x.Quantity > 0 &&
                        x.CreatedAt > lastAlert.CreatedAt,
                        cancellationToken);
                var sameLevelRecentlyAlerted = lastAlert?.Type == type.Value &&
                    lastAlert.CreatedAt >= now.AddHours(-24) && !replenishedAfterAlert;
                if (sameLevelRecentlyAlerted) continue;

                var title = type.Value switch
                {
                    NotificationType.StockLow => "Stock bajo",
                    NotificationType.StockCritical => "Stock crítico",
                    NotificationType.OutOfStock => "Producto agotado",
                    _ => "Alerta de inventario"
                };
                var body = type.Value switch
                {
                    NotificationType.StockLow => $"{product.Name}: quedan {ColombianFormat.Number(product.CurrentStock)} unidades. Se recomienda hacer una reposición.",
                    NotificationType.StockCritical => $"{product.Name}: quedan {ColombianFormat.Number(product.CurrentStock)} unidades. Reposición prioritaria requerida.",
                    NotificationType.OutOfStock => $"{product.Name}: el inventario llegó a 0 unidades.",
                    _ => product.Name
                };

                var ids = await notificationService.CreateForOwnersAsync(
                    tenant.Id, type.Value, title, body, "Product", product.Id, now, null, cancellationToken);
                pendingDeliveryIds.AddRange(ids);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (var id in pendingDeliveryIds)
            backgroundJobs.Enqueue<NotificationJobs>(job => job.DeliverAsync(id));
    }

    private static NotificationType? GetAlertType(decimal current, decimal minimum, decimal critical)
    {
        if (current <= 0) return NotificationType.OutOfStock;
        if (current <= critical) return NotificationType.StockCritical;
        if (current <= minimum) return NotificationType.StockLow;
        return null;
    }
}
