using BillaresPos.Application.Common.Abstractions;
using BillaresPos.Domain.Notifications;
using BillaresPos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillaresPos.Api.Notifications;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notifications")
            .RequireAuthorization("CashierOrAbove");

        group.MapGet("", async (
            bool? unreadOnly,
            int? pageSize,
            AppDbContext dbContext,
            ICurrentUser currentUser,
            CancellationToken cancellationToken) =>
        {
            var size = Math.Clamp(pageSize ?? 50, 1, 100);
            var query = dbContext.Notifications.AsNoTracking()
                .Where(x => x.RecipientUserId == currentUser.UserId)
                .Where(x => x.Channel == NotificationChannel.InApp);

            if (unreadOnly == true)
            {
                query = query.Where(x => x.ReadAt == null);
            }

            var notifications = await query
                .OrderByDescending(x => x.CreatedAt)
                .Take(size)
                .Select(x => new NotificationResponse(
                    x.Id, x.Channel, x.Type, x.Status, x.Title, x.Body,
                    x.ReferenceType, x.ReferenceId, x.CreatedAt, x.SentAt, x.ReadAt))
                .ToListAsync(cancellationToken);

            return Results.Ok(notifications);
        });

        group.MapPost("/{id:guid}/read", async (
            Guid id,
            AppDbContext dbContext,
            ICurrentUser currentUser,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            var notification = await dbContext.Notifications
                .SingleOrDefaultAsync(x => x.Id == id && x.RecipientUserId == currentUser.UserId, cancellationToken);

            if (notification is null)
            {
                return Results.NotFound();
            }

            notification.ReadAt ??= clock.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });
    }
}

public sealed record NotificationResponse(
    Guid Id,
    NotificationChannel Channel,
    NotificationType Type,
    NotificationStatus Status,
    string Title,
    string Body,
    string? ReferenceType,
    Guid? ReferenceId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    DateTimeOffset? ReadAt);
