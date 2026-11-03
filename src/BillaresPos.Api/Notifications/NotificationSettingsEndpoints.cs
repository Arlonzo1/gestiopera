using BillaresPos.Application.Common.Abstractions;
using BillaresPos.Domain.Notifications;
using BillaresPos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BillaresPos.Api.Notifications;

public static class NotificationSettingsEndpoints
{
    public static void MapNotificationSettingsEndpoints(this WebApplication app)
    {
        var preferences = app.MapGroup("/api/notification-preferences")
            .RequireAuthorization("AdministratorOrOwner");

        preferences.MapGet("", async (AppDbContext db, ICurrentUser user, ICurrentTenant tenant, CancellationToken ct) =>
        {
            var preference = await db.NotificationPreferences.AsNoTracking()
                .SingleOrDefaultAsync(x => x.UserId == user.UserId && x.TenantId == tenant.TenantId, ct);
            return Results.Ok(preference is null
                ? new NotificationPreferenceResponse(SaleNotificationMode.AllSales, 0, true, false, null, null)
                : new NotificationPreferenceResponse(preference.SaleNotificationMode, preference.SaleNotificationAmount, preference.PushEnabled, preference.WhatsAppEnabled, preference.WhatsAppPhoneNumber, preference.EmailAddress));
        });

        preferences.MapPut("", async (
            UpdateNotificationPreferenceRequest request,
            AppDbContext db,
            ICurrentUser user,
            ICurrentTenant tenant,
            IClock clock,
            CancellationToken ct) =>
        {
            if (tenant.TenantId is null || user.UserId is null || request.SaleNotificationAmount < 0)
            {
                return Results.BadRequest(new { detail = "La configuración de notificaciones no es válida." });
            }

            var preference = await db.NotificationPreferences
                .SingleOrDefaultAsync(x => x.UserId == user.UserId && x.TenantId == tenant.TenantId, ct);
            if (preference is null)
            {
                preference = new NotificationPreference
                {
                    Id = Guid.NewGuid(), TenantId = tenant.TenantId.Value, UserId = user.UserId.Value
                };
                db.NotificationPreferences.Add(preference);
            }

            preference.SaleNotificationMode = request.SaleNotificationMode;
            preference.SaleNotificationAmount = request.SaleNotificationAmount;
            preference.PushEnabled = request.PushEnabled;
            preference.WhatsAppEnabled = request.WhatsAppEnabled;
            preference.WhatsAppPhoneNumber = string.IsNullOrWhiteSpace(request.WhatsAppPhoneNumber) ? null : request.WhatsAppPhoneNumber.Trim();
            preference.EmailAddress = string.IsNullOrWhiteSpace(request.EmailAddress) ? null : request.EmailAddress.Trim();
            preference.UpdatedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new NotificationPreferenceResponse(preference.SaleNotificationMode, preference.SaleNotificationAmount, preference.PushEnabled, preference.WhatsAppEnabled, preference.WhatsAppPhoneNumber, preference.EmailAddress));
        });

        var devices = app.MapGroup("/api/notification-devices")
            .RequireAuthorization("CashierOrAbove");

        devices.MapPost("", async (
            RegisterPushDeviceRequest request,
            AppDbContext db,
            ICurrentUser user,
            ICurrentTenant tenant,
            IClock clock,
            CancellationToken ct) =>
        {
            if (tenant.TenantId is null || user.UserId is null || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.DeviceId))
            {
                return Results.BadRequest(new { detail = "Token y dispositivo son obligatorios." });
            }

            var device = await db.PushDeviceTokens.SingleOrDefaultAsync(
                x => x.TenantId == tenant.TenantId && x.UserId == user.UserId && x.DeviceId == request.DeviceId, ct);
            if (device is null)
            {
                device = new PushDeviceToken
                {
                    Id = Guid.NewGuid(), TenantId = tenant.TenantId.Value, UserId = user.UserId.Value,
                    DeviceId = request.DeviceId.Trim()
                };
                db.PushDeviceTokens.Add(device);
            }

            device.Token = request.Token.Trim();
            device.Platform = string.IsNullOrWhiteSpace(request.Platform) ? "web" : request.Platform.Trim();
            device.IsActive = true;
            device.LastSeenAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { device.Id, device.DeviceId, device.Platform, device.IsActive });
        });

        devices.MapDelete("/{deviceId}", async (
            string deviceId,
            AppDbContext db,
            ICurrentUser user,
            ICurrentTenant tenant,
            CancellationToken ct) =>
        {
            var device = await db.PushDeviceTokens.SingleOrDefaultAsync(
                x => x.TenantId == tenant.TenantId && x.UserId == user.UserId && x.DeviceId == deviceId, ct);
            if (device is null) return Results.NotFound();
            device.IsActive = false;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }
}

public sealed record UpdateNotificationPreferenceRequest(
    SaleNotificationMode SaleNotificationMode,
    decimal SaleNotificationAmount,
    bool PushEnabled,
    bool WhatsAppEnabled,
    string? WhatsAppPhoneNumber,
    string? EmailAddress);

public sealed record NotificationPreferenceResponse(
    SaleNotificationMode SaleNotificationMode,
    decimal SaleNotificationAmount,
    bool PushEnabled,
    bool WhatsAppEnabled,
    string? WhatsAppPhoneNumber,
    string? EmailAddress);

public sealed record RegisterPushDeviceRequest(string Token, string DeviceId, string? Platform);
