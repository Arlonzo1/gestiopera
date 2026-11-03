using System.Text.Json;
using BillaresPos.Application.Common.Abstractions;
using BillaresPos.Application.Common.Exceptions;
using BillaresPos.Domain.Auditing;
using BillaresPos.Domain.Identity;
using BillaresPos.Infrastructure.Identity;
using BillaresPos.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BillaresPos.Api.Users;

public sealed record EmployeeResponse(Guid Id, string Email, string FullName, string[] Roles, bool IsActive, DateTimeOffset CreatedAt);
public sealed record CreateEmployeeRequest(string Email, string FullName, string Password, string Role);
public sealed record ChangeEmployeeRoleRequest(string Role);
public sealed record ChangeEmployeeStatusRequest(bool IsActive);
public sealed record AuditLogResponse(Guid Id, Guid? UserId, string Action, string EntityType, Guid? EntityId, string? OldValuesJson, string? NewValuesJson, string? IpAddress, DateTimeOffset CreatedAt);

public static class UserManagementEndpoints
{
    public static void MapUserManagementEndpoints(this WebApplication app)
    {
        var users = app.MapGroup("/api/users").RequireAuthorization("AdministratorOrOwner").WithTags("Users");
        users.MapGet("", ListAsync);
        users.MapPost("", CreateAsync);
        users.MapPatch("/{id:guid}/role", ChangeRoleAsync);
        users.MapPatch("/{id:guid}/status", ChangeStatusAsync);

        app.MapGet("/api/audit", ListAuditAsync)
            .RequireAuthorization("AdministratorOrOwner")
            .WithTags("Auditing");
    }

    private static async Task<IResult> ListAsync(AppDbContext db, UserManager<ApplicationUser> manager, CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().OrderBy(x => x.FullName).ToListAsync(ct);
        var result = new List<EmployeeResponse>(users.Count);
        foreach (var user in users)
            result.Add(new EmployeeResponse(user.Id, user.Email ?? string.Empty, user.FullName, (await manager.GetRolesAsync(user)).ToArray(), user.IsActive, user.CreatedAt));
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateAsync(
        CreateEmployeeRequest request,
        AppDbContext db,
        UserManager<ApplicationUser> manager,
        ICurrentTenant tenant,
        ICurrentUser currentUser,
        IHttpContextAccessor http,
        IClock clock,
        CancellationToken ct)
    {
        if (tenant.TenantId is null || currentUser.UserId is null)
            throw new ForbiddenException("Se requiere un usuario autenticado del tenant.");
        var role = NormalizeRole(request.Role);
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Password) || role is null)
            throw new ValidationAppException("Los datos del empleado no son validos.", new Dictionary<string, string[]> { ["employee"] = ["Correo, nombre, contraseña y rol son obligatorios."] });
        if (role == RoleNames.Owner && !http.HttpContext!.User.IsInRole(RoleNames.Owner))
            throw new ForbiddenException("Solo OWNER puede crear otro OWNER.");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), TenantId = tenant.TenantId.Value, Email = request.Email.Trim(), UserName = request.Email.Trim(),
            FullName = request.FullName.Trim(), IsActive = true, CreatedAt = clock.UtcNow, UpdatedAt = clock.UtcNow
        };
        var creation = await manager.CreateAsync(user, request.Password);
        if (!creation.Succeeded)
            throw new ValidationAppException("No se pudo crear el empleado.", creation.Errors.GroupBy(x => x.Code).ToDictionary(x => x.Key, x => x.Select(TranslateIdentityError).ToArray()));
        var roleResult = await manager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
            throw new ValidationAppException("No se pudo asignar el rol.", roleResult.Errors.GroupBy(x => x.Code).ToDictionary(x => x.Key, x => x.Select(TranslateIdentityError).ToArray()));

        db.AuditLogs.Add(CreateAudit(tenant.TenantId.Value, currentUser.UserId.Value, "USER_CREATED", "User", user.Id, null, new { user.Email, user.FullName, Role = role }, http, clock));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/users/{user.Id}", new EmployeeResponse(user.Id, user.Email!, user.FullName, [role], true, user.CreatedAt));
    }

    private static async Task<IResult> ChangeRoleAsync(
        Guid id,
        ChangeEmployeeRoleRequest request,
        AppDbContext db,
        UserManager<ApplicationUser> manager,
        ICurrentTenant tenant,
        ICurrentUser currentUser,
        IHttpContextAccessor http,
        IClock clock,
        CancellationToken ct)
    {
        if (tenant.TenantId is null || currentUser.UserId is null)
            throw new ForbiddenException("Se requiere un usuario autenticado del tenant.");
        var role = NormalizeRole(request.Role);
        if (role is null)
            throw new ValidationAppException("El rol no es valido.", new Dictionary<string, string[]> { ["role"] = ["Use OWNER, ADMINISTRATOR o CASHIER."] });
        if (role == RoleNames.Owner && !http.HttpContext!.User.IsInRole(RoleNames.Owner))
            throw new ForbiddenException("Solo OWNER puede asignar OWNER.");
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) throw new NotFoundException("Empleado no encontrado.");
        var oldRoles = await manager.GetRolesAsync(user);
        if (oldRoles.Contains(RoleNames.Owner) && !http.HttpContext!.User.IsInRole(RoleNames.Owner))
            throw new ForbiddenException("Solo OWNER puede modificar un OWNER.");
        var result = await manager.RemoveFromRolesAsync(user, oldRoles);
        if (!result.Succeeded) throw new ValidationAppException("No se pudo actualizar el rol.", new Dictionary<string, string[]> { ["role"] = ["Identity no pudo actualizar el rol."] });
        result = await manager.AddToRoleAsync(user, role);
        if (!result.Succeeded) throw new ValidationAppException("No se pudo actualizar el rol.", new Dictionary<string, string[]> { ["role"] = ["Identity no pudo actualizar el rol."] });
        user.UpdatedAt = clock.UtcNow;
        db.AuditLogs.Add(CreateAudit(tenant.TenantId.Value, currentUser.UserId.Value, "USER_ROLE_CHANGED", "User", user.Id, new { Roles = oldRoles }, new { Role = role }, http, clock));
        await db.SaveChangesAsync(ct);
        return Results.Ok(new EmployeeResponse(user.Id, user.Email ?? string.Empty, user.FullName, [role], user.IsActive, user.CreatedAt));
    }

    private static async Task<IResult> ChangeStatusAsync(
        Guid id,
        ChangeEmployeeStatusRequest request,
        AppDbContext db,
        UserManager<ApplicationUser> manager,
        ICurrentTenant tenant,
        ICurrentUser currentUser,
        IHttpContextAccessor http,
        IClock clock,
        CancellationToken ct)
    {
        if (tenant.TenantId is null || currentUser.UserId is null)
            throw new ForbiddenException("Se requiere un usuario autenticado del tenant.");
        if (id == currentUser.UserId.Value)
            throw new ValidationAppException("No puedes desactivar tu propio usuario.", new Dictionary<string, string[]> { ["user"] = ["El usuario actualmente conectado no se puede desactivar."] });

        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (user is null) throw new NotFoundException("Empleado no encontrado.");
        var roles = await manager.GetRolesAsync(user);
        if (roles.Contains(RoleNames.Owner) && !http.HttpContext!.User.IsInRole(RoleNames.Owner))
            throw new ForbiddenException("Solo OWNER puede modificar un OWNER.");
        if (user.IsActive == request.IsActive)
            return Results.Ok(new EmployeeResponse(user.Id, user.Email ?? string.Empty, user.FullName, roles.ToArray(), user.IsActive, user.CreatedAt));

        var previous = user.IsActive;
        user.IsActive = request.IsActive;
        user.UpdatedAt = clock.UtcNow;
        db.AuditLogs.Add(CreateAudit(tenant.TenantId.Value, currentUser.UserId.Value,
            request.IsActive ? "USER_ACTIVATED" : "USER_DEACTIVATED", "User", user.Id,
            new { IsActive = previous }, new { IsActive = request.IsActive }, http, clock));
        await db.SaveChangesAsync(ct);
        return Results.Ok(new EmployeeResponse(user.Id, user.Email ?? string.Empty, user.FullName, roles.ToArray(), user.IsActive, user.CreatedAt));
    }
    private static async Task<IResult> ListAuditAsync(int? page, int? pageSize, AppDbContext db, CancellationToken ct)
    {
        var safePage = Math.Max(1, page ?? 1);
        var safeSize = Math.Clamp(pageSize ?? 50, 1, 100);
        var logs = (await db.AuditLogs.AsNoTracking().ToListAsync(ct))
            .OrderByDescending(x => x.CreatedAt)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .Select(x => new AuditLogResponse(x.Id, x.UserId, x.Action, x.EntityType, x.EntityId, x.OldValuesJson, x.NewValuesJson, x.IpAddress, x.CreatedAt))
            .ToList();
        return Results.Ok(logs);
    }

    private static AuditLog CreateAudit(Guid tenantId, Guid userId, string action, string entityType, Guid entityId, object? oldValues, object? newValues, IHttpContextAccessor http, IClock clock) =>
        new()
        {
            Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId, Action = action, EntityType = entityType, EntityId = entityId,
            OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues),
            IpAddress = http.HttpContext?.Connection.RemoteIpAddress?.ToString(), CreatedAt = clock.UtcNow
        };

    private static string TranslateIdentityError(IdentityError error) => error.Code switch
    {
        "PasswordTooShort" => "La contraseña debe tener al menos 10 caracteres.",
        "PasswordRequiresNonAlphanumeric" => "La contraseña debe incluir al menos un carácter especial.",
        "PasswordRequiresDigit" => "La contraseña debe incluir al menos un número.",
        "PasswordRequiresLower" => "La contraseña debe incluir al menos una letra minúscula.",
        "PasswordRequiresUpper" => "La contraseña debe incluir al menos una letra mayúscula.",
        "DuplicateUserName" or "DuplicateEmail" => "Ya existe un usuario con ese correo.",
        _ => error.Description
    };
    private static string? NormalizeRole(string? role) => role?.Trim().ToUpperInvariant() switch
    {
        RoleNames.Owner => RoleNames.Owner,
        RoleNames.Administrator => RoleNames.Administrator,
        RoleNames.Cashier => RoleNames.Cashier,
        _ => null
    };
}