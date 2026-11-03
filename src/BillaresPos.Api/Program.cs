using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using BillaresPos.Api.Auth;
using BillaresPos.Api.Billiards;
using BillaresPos.Api.CashManagement;
using BillaresPos.Api.Dashboard;
using BillaresPos.Api.Errors;
using BillaresPos.Api.Inventory;
using BillaresPos.Api.Jobs;
using BillaresPos.Api.Notifications;
using BillaresPos.Api.Tenancy;
using BillaresPos.Api.Users;
using BillaresPos.Api.Products;
using BillaresPos.Api.Reports;
using BillaresPos.Api.Purchases;
using BillaresPos.Api.Sales;
using BillaresPos.Application.Common.Abstractions;
using BillaresPos.Domain.Auditing;
using BillaresPos.Domain.Identity;
using BillaresPos.Domain.Inventory;
using BillaresPos.Domain.Products;
using BillaresPos.Domain.Purchases;
using BillaresPos.Domain.Reports;
using BillaresPos.Domain.Notifications;
using BillaresPos.Domain.Sales;
using BillaresPos.Application.Common.Exceptions;
using BillaresPos.Infrastructure;
using BillaresPos.Infrastructure.Identity;
using BillaresPos.Infrastructure.Persistence;
using FluentValidation;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey must be configured and at least 32 characters long.");
}

builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentTenant>();
builder.Services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<CurrentTenant>());
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<DevelopmentDataSeeder>();

builder.Services.AddScoped<PdfGenerationJobs>();
    builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<NotificationJobs>();
builder.Services.AddScoped<InventoryAlertJobs>();
var hangfireConnection = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection is required for Hangfire.");
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHangfire(configuration => configuration
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(hangfireConnection)));
    builder.Services.AddHangfireServer();
}
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddPostgresPersistence(builder.Configuration);
}
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("OwnerOnly", policy => policy.RequireRole(RoleNames.Owner));
    options.AddPolicy("AdministratorOrOwner", policy => policy.RequireRole(RoleNames.Owner, RoleNames.Administrator));
    options.AddPolicy("CashierOrAbove", policy => policy.RequireRole(RoleNames.Owner, RoleNames.Administrator, RoleNames.Cashier));
});
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddOpenApi();
builder.Services.AddSignalR();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Length == 0 && builder.Environment.IsDevelopment()) allowedOrigins = ["http://localhost:5173"];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>().SeedAsync();
}
else if (app.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();

    if (app.Configuration.GetValue<bool>("Database:SeedDemoData"))
    {
        await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>().SeedAsync();
    }
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
    app.UseHangfireDashboard("/hangfire");

app.MapBilliardsEndpoints();
app.MapCashManagementEndpoints();
app.MapDashboardEndpoints();
app.MapPhysicalInventoryEndpoints();
app.MapReportEndpoints();
app.MapReportPdfEndpoints();
app.MapSalePdfEndpoints();
app.MapUserManagementEndpoints();
app.MapNotificationEndpoints();
app.MapNotificationSettingsEndpoints();
app.MapTenantBrandingEndpoints();

var auth = app.MapGroup("/api/auth").WithTags("Auth");

auth.MapPost("/login", async (
    LoginRequest request,
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    Microsoft.Extensions.Options.IOptions<JwtOptions> tokenOptions,
    IClock clock,
    IHttpContextAccessor http,
    CancellationToken cancellationToken) =>
{
    var normalizedEmail = request.Email.Trim().ToUpperInvariant();
    var candidates = await dbContext.Users.IgnoreQueryFilters()
        .Where(x => x.NormalizedEmail == normalizedEmail && x.IsActive)
        .Join(dbContext.Tenants.AsNoTracking().Where(x => x.IsActive), user => user.TenantId, tenant => tenant.Id, (user, tenant) => new { User = user, Tenant = tenant })
        .Where(x => string.IsNullOrWhiteSpace(request.TenantSlug) || x.Tenant.Slug == request.TenantSlug)
        .ToListAsync(cancellationToken);
    if (candidates.Count != 1)
    {
        return Results.Unauthorized();
    }

    var candidate = candidates[0];
    var tenant = candidate.Tenant;
    var user = candidate.User;

    var passwordValid = await userManager.CheckPasswordAsync(user, request.Password);
    if (!passwordValid)
    {
        await userManager.AccessFailedAsync(user);
        return Results.Unauthorized();
    }
    await userManager.ResetAccessFailedCountAsync(user);

    var response = await tokenService.CreateTokenResponseAsync(user, cancellationToken);
    dbContext.RefreshTokens.Add(new RefreshToken
    {
        Id = Guid.NewGuid(),
        TenantId = user.TenantId,
        UserId = user.Id,
        TokenHash = tokenService.HashRefreshToken(response.RefreshToken),
        CreatedAt = clock.UtcNow,
        ExpiresAt = clock.UtcNow.AddDays(tokenOptions.Value.RefreshTokenDays)
    });
    dbContext.AuditLogs.Add(new AuditLog
    {
        Id = Guid.NewGuid(), TenantId = user.TenantId, UserId = user.Id, Action = "LOGIN", EntityType = "User", EntityId = user.Id,
        IpAddress = http.HttpContext?.Connection.RemoteIpAddress?.ToString(), CreatedAt = clock.UtcNow
    });
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(response);
});

auth.MapPost("/refresh", async (
    RefreshTokenRequest request,
    AppDbContext dbContext,
    ITokenService tokenService,
    Microsoft.Extensions.Options.IOptions<JwtOptions> tokenOptions,
    IClock clock,
    CancellationToken cancellationToken) =>
{
    var hash = tokenService.HashRefreshToken(request.RefreshToken);
    var stored = await dbContext.RefreshTokens.IgnoreQueryFilters()
        .Include(x => x.User)
        .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
    if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt <= clock.UtcNow || !stored.User.IsActive)
    {
        return Results.Unauthorized();
    }

    var response = await tokenService.CreateTokenResponseAsync(stored.User, cancellationToken);
    stored.RevokedAt = clock.UtcNow;
    stored.ReplacedByTokenHash = tokenService.HashRefreshToken(response.RefreshToken);
    dbContext.RefreshTokens.Add(new RefreshToken
    {
        Id = Guid.NewGuid(),
        TenantId = stored.TenantId,
        UserId = stored.UserId,
        TokenHash = stored.ReplacedByTokenHash,
        CreatedAt = clock.UtcNow,
        ExpiresAt = clock.UtcNow.AddDays(tokenOptions.Value.RefreshTokenDays)
    });
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(response);
});

var tenants = app.MapGroup("/api/tenants").RequireAuthorization().WithTags("Tenancy");
tenants.MapGet("/current", (ICurrentTenant currentTenant) => Results.Ok(new
{
    currentTenant.TenantId,
    currentTenant.Slug,
    currentTenant.TimeZoneId,
    currentTenant.Currency
}));

var users = app.MapGroup("/api/users").RequireAuthorization("AdministratorOrOwner").WithTags("Users");
users.MapGet("/{id:guid}", async (Guid id, AppDbContext dbContext, UserManager<ApplicationUser> userManager, CancellationToken cancellationToken) =>
{
    var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (user is null)
    {
        return Results.NotFound();
    }

    var roles = await userManager.GetRolesAsync(user);
    return Results.Ok(new UserProfile(user.Id, user.TenantId, user.Email ?? string.Empty, user.FullName, roles.ToArray()));
});



var purchases = app.MapGroup("/api/purchases").RequireAuthorization("AdministratorOrOwner").WithTags("Purchases");

purchases.MapPost("", async (
    CreatePurchaseRequest request,
    AppDbContext dbContext,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IClock clock,
    IServiceProvider services,
    NotificationService notificationService,
    CancellationToken cancellationToken) =>
{
    if (currentTenant.TenantId is null || currentUser.UserId is null)
    {
        throw new ForbiddenException("An authenticated tenant user is required.");
    }

    if (request.Items is null || request.Items.Count == 0 ||
        request.PurchaseDate == default ||
        request.Items.Any(x => x.Quantity <= 0 || decimal.Truncate(x.Quantity) != x.Quantity || x.UnitCost < 0 || decimal.Truncate(x.UnitCost) != x.UnitCost))
    {
        throw new ValidationAppException(
            "Invalid purchase data.",
            new Dictionary<string, string[]>
            {
                ["purchase"] = ["At least one item is required. Date, quantity and unit cost must be valid."]
            });
    }

    await using var transaction = await dbContext.Database.BeginTransactionAsync(
        System.Data.IsolationLevel.Serializable, cancellationToken);

    var productIds = request.Items.Select(x => x.ProductId).Distinct().ToArray();
    var productsById = await dbContext.Products
        .Where(x => productIds.Contains(x.Id) && x.IsActive)
        .ToDictionaryAsync(x => x.Id, cancellationToken);

    if (productsById.Count != productIds.Length)
    {
        throw new NotFoundException("One or more products were not found in the current tenant.");
    }

    var purchase = new Purchase
    {
        Id = Guid.NewGuid(),
        TenantId = currentTenant.TenantId.Value,
        Supplier = string.IsNullOrWhiteSpace(request.Supplier) ? null : request.Supplier.Trim(),
        InvoiceReference = string.IsNullOrWhiteSpace(request.InvoiceReference) ? null : request.InvoiceReference.Trim(),
        PurchaseDate = request.PurchaseDate,
        CreatedByUserId = currentUser.UserId.Value,
        CreatedAt = clock.UtcNow
    };

    var aggregated = request.Items
        .GroupBy(x => x.ProductId)
        .ToDictionary(
            group => group.Key,
            group => new
            {
                Quantity = group.Sum(x => x.Quantity),
                TotalCost = group.Sum(x => x.Quantity * x.UnitCost)
            });

    foreach (var item in request.Items)
    {
        var subtotal = item.Quantity * item.UnitCost;
        purchase.Items.Add(new PurchaseItem
        {
            Id = Guid.NewGuid(),
            TenantId = purchase.TenantId,
            PurchaseId = purchase.Id,
            ProductId = item.ProductId,
            Quantity = item.Quantity,
            UnitCost = item.UnitCost,
            Subtotal = subtotal
        });

        dbContext.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(),
            TenantId = purchase.TenantId,
            ProductId = item.ProductId,
            MovementType = InventoryMovementType.Purchase,
            Quantity = item.Quantity,
            UnitCost = item.UnitCost,
            ReferenceType = "Purchase",
            ReferenceId = purchase.Id,
            CreatedByUserId = purchase.CreatedByUserId,
            CreatedAt = purchase.CreatedAt
        });
    }

    foreach (var (productId, aggregate) in aggregated)
    {
        var product = productsById[productId];
        var previousValue = product.CurrentStock * product.AverageCost;
        var newStock = product.CurrentStock + aggregate.Quantity;
        product.AverageCost = newStock == 0
            ? 0
            : decimal.Round((previousValue + aggregate.TotalCost) / newStock, 2, MidpointRounding.ToEven);
        product.CurrentStock = newStock;
    }

    purchase.Total = purchase.Items.Sum(x => x.Subtotal);
    dbContext.Purchases.Add(purchase);
    await dbContext.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);

    var notificationIds = await notificationService.CreateForOwnersAsync(
        purchase.TenantId, NotificationType.PurchaseCompleted,
        "Compra recibida",
        $"Se registrÃƒÂ³ una compra por {purchase.Total:N0} COP con {purchase.Items.Count} producto(s). El inventario fue actualizado.",
        "Purchase", purchase.Id, purchase.CreatedAt, null, cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);
    if (services.GetService<IBackgroundJobClient>() is { } backgroundJobs)
    {
        foreach (var notificationId in notificationIds)
            backgroundJobs.Enqueue<NotificationJobs>(job => job.DeliverAsync(notificationId));
    }

    return Results.Created($"/api/purchases/{purchase.Id}",
        new PurchaseResponse(purchase.Id, purchase.PurchaseDate, purchase.Total, purchase.Items.Count));
});


var sales = app.MapGroup("/api/sales").RequireAuthorization("CashierOrAbove").WithTags("Sales");

sales.MapGet("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
{
    var sale = await db.Sales.AsNoTracking()
        .Include(x => x.Items)
        .Include(x => x.Payments)
        .SingleOrDefaultAsync(x => x.Id == id, ct);
    if (sale is null) throw new NotFoundException("Venta no encontrada.");

    var cashierName = await db.Users.AsNoTracking()
        .Where(x => x.Id == sale.UserId && x.TenantId == sale.TenantId)
        .Select(x => x.FullName)
        .SingleOrDefaultAsync(ct);

    string? tableName = null;
    int? billiardMinutes = sale.Items
        .Where(x => x.ItemType == SaleItemType.BilliardTime)
        .Select(x => (int?)x.Quantity)
        .FirstOrDefault();

    const string sessionPrefix = "Billiard session ";
    if (sale.Notes?.StartsWith(sessionPrefix, StringComparison.Ordinal) == true &&
        Guid.TryParse(sale.Notes[sessionPrefix.Length..], out var sessionId))
    {
        tableName = await db.BilliardSessions.AsNoTracking()
            .Where(x => x.Id == sessionId && x.TenantId == sale.TenantId)
            .Join(db.BilliardTables.AsNoTracking(), session => session.BilliardTableId, table => table.Id, (_, table) => table.Name)
            .SingleOrDefaultAsync(ct);
    }

    return Results.Ok(new SaleDetailResponse(
        sale.Id,
        sale.SequentialNumber,
        sale.CreatedAt,
        sale.Status.ToString(),
        sale.Subtotal,
        sale.Discount,
        sale.Total,
        cashierName,
        sale.CustomerName,
        tableName,
        billiardMinutes,
        sale.Items.OrderBy(x => x.Id).Select(x => new SaleDetailItemResponse(
            x.DescriptionSnapshot, x.Quantity, x.UnitPrice, x.Subtotal, x.ItemType.ToString())).ToList(),
        sale.Payments.Select(x => new SaleDetailPaymentResponse(x.Method.ToString(), x.Amount)).ToList()));
});
sales.MapPost("", async (
    CreateSaleRequest request,
    AppDbContext dbContext,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IClock clock,
    NotificationService notificationService,
    IServiceProvider services,
    CancellationToken cancellationToken) =>
{
    if (currentTenant.TenantId is null || currentUser.UserId is null)
    {
        throw new ForbiddenException("An authenticated tenant user is required.");
    }

    if (request.Items is null || request.Items.Count == 0 ||
        request.Payments is null || request.Payments.Count == 0 ||
        request.Discount < 0 ||
        request.Items.Any(x => x.Quantity <= 0 || decimal.Truncate(x.Quantity) != x.Quantity) ||
        request.Payments.Any(x => x.Amount <= 0 || decimal.Truncate(x.Amount) != x.Amount) ||
        decimal.Truncate(request.Discount) != request.Discount)
    {
        throw new ValidationAppException(
            "Invalid sale data.",
            new Dictionary<string, string[]>
            {
                ["sale"] = ["At least one item and payment are required. Quantities, payments and discount must be positive or zero."]
            });
    }

    await using var transaction = await dbContext.Database.BeginTransactionAsync(
        System.Data.IsolationLevel.Serializable, cancellationToken);

    var productIds = request.Items.Select(x => x.ProductId).Distinct().ToArray();
    var productsById = await dbContext.Products
        .Where(x => productIds.Contains(x.Id) && x.IsActive)
        .ToDictionaryAsync(x => x.Id, cancellationToken);

    if (productsById.Count != productIds.Length)
    {
        throw new NotFoundException("One or more products were not found in the current tenant.");
    }

    var quantitiesByProduct = request.Items
        .GroupBy(x => x.ProductId)
        .ToDictionary(x => x.Key, x => x.Sum(item => item.Quantity));

    foreach (var (productId, quantity) in quantitiesByProduct)
    {
        if (productsById[productId].CurrentStock < quantity)
        {
            throw new ValidationAppException(
                "Insufficient inventory.",
                new Dictionary<string, string[]>
                {
                    ["quantity"] = [$"Product {productId} does not have enough stock."]
                });
        }
    }

    var subtotal = request.Items.Sum(item =>
        item.Quantity * productsById[item.ProductId].SalePrice);
    var total = decimal.Round(subtotal - request.Discount, 2, MidpointRounding.ToEven);
    if (total < 0)
    {
        throw new ValidationAppException(
            "Invalid discount.",
            new Dictionary<string, string[]> { ["discount"] = ["Discount cannot exceed the subtotal."] });
    }

    var paymentsTotal = decimal.Round(request.Payments.Sum(x => x.Amount), 2, MidpointRounding.ToEven);
    if (paymentsTotal != total)
    {
        throw new ValidationAppException(
            "Payment total does not match sale total.",
            new Dictionary<string, string[]> { ["payments"] = ["Payment amounts must equal the sale total."] });
    }

    var activeCashShiftId = await dbContext.CashShifts.Where(x => x.UserId == currentUser.UserId && x.Status == BillaresPos.Domain.CashManagement.CashShiftStatus.Open).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);

    var nextNumber = (await dbContext.Sales
        .Select(x => (long?)x.SequentialNumber)
        .MaxAsync(cancellationToken) ?? 0) + 1;

    var sale = new Sale
    {
        Id = Guid.NewGuid(),
        TenantId = currentTenant.TenantId.Value,
        SequentialNumber = nextNumber,
        UserId = currentUser.UserId.Value,
        CashShiftId = activeCashShiftId,
        CreatedAt = clock.UtcNow,
        Status = SaleStatus.Completed,
        Subtotal = decimal.Round(subtotal, 2),
        Discount = request.Discount,
        Total = total,
        CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? null : request.CustomerName.Trim(),
        Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
    };

    foreach (var item in request.Items)
    {
        var product = productsById[item.ProductId];
        sale.Items.Add(new SaleItem
        {
            Id = Guid.NewGuid(),
            TenantId = sale.TenantId,
            SaleId = sale.Id,
            ProductId = product.Id,
            DescriptionSnapshot = product.Name,
            Quantity = item.Quantity,
            UnitPrice = product.SalePrice,
            UnitCostAtSale = product.AverageCost,
            Subtotal = decimal.Round(item.Quantity * product.SalePrice, 2),
            ItemType = SaleItemType.Product
        });
    }

    foreach (var payment in request.Payments)
    {
        sale.Payments.Add(new SalePayment
        {
            Id = Guid.NewGuid(),
            TenantId = sale.TenantId,
            SaleId = sale.Id,
            Method = payment.Method,
            Amount = payment.Amount
        });
    }

    foreach (var (productId, quantity) in quantitiesByProduct)
    {
        var product = productsById[productId];
        product.CurrentStock -= quantity;
        dbContext.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(),
            TenantId = sale.TenantId,
            ProductId = productId,
            MovementType = InventoryMovementType.Sale,
            Quantity = -quantity,
            UnitCost = product.AverageCost,
            ReferenceType = "Sale",
            ReferenceId = sale.Id,
            CreatedByUserId = sale.UserId,
            CreatedAt = sale.CreatedAt
        });
    }

    dbContext.Sales.Add(sale);
    var receiptDocument = new GeneratedDocument
    {
        Id = Guid.NewGuid(),
        TenantId = sale.TenantId,
        DocumentType = "SALE_RECEIPT",
        ReferenceId = sale.Id,
        FileName = $"comprobante-venta-{sale.SequentialNumber}.pdf",
        Status = GeneratedDocumentStatus.Pending,
        CreatedAt = sale.CreatedAt
    };
    dbContext.GeneratedDocuments.Add(receiptDocument);
    var notificationIds = await notificationService.CreateForOwnersAsync(
        sale.TenantId,
        NotificationType.SaleCompleted,
        "Nueva venta",
        $"Venta #{sale.SequentialNumber} por {sale.Total:N0} COP.",
        "Sale",
        sale.Id,
        sale.CreatedAt,
        sale.Total,
        cancellationToken);
    await dbContext.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    var backgroundJobs = services.GetService<IBackgroundJobClient>();
    var pdfJobId = backgroundJobs?.Enqueue<PdfGenerationJobs>(job => job.GenerateSaleReceiptAsync(receiptDocument.Id));
    backgroundJobs?.Enqueue<InventoryAlertJobs>(job => job.CheckAsync(CancellationToken.None));
    if (backgroundJobs is not null && pdfJobId is not null)
    {
        foreach (var notificationId in notificationIds)
        {
            backgroundJobs.ContinueJobWith<NotificationJobs>(
                pdfJobId,
                job => job.DeliverAsync(notificationId));
        }
    }
    return Results.Created($"/api/sales/{sale.Id}",
        new SaleResponse(sale.Id, sale.SequentialNumber, sale.Subtotal, sale.Discount,
            sale.Total, sale.Status, sale.Items.Count));
});

sales.MapPost("/{id:guid}/cancel", async (
    Guid id,
    CancelSaleRequest request,
    AppDbContext dbContext,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IClock clock,
    NotificationService notificationService,
    IServiceProvider services,
    CancellationToken cancellationToken) =>
{
    if (currentTenant.TenantId is null || currentUser.UserId is null)
    {
        throw new ForbiddenException("An authenticated tenant user is required.");
    }

    if (string.IsNullOrWhiteSpace(request.Reason))
    {
        throw new ValidationAppException(
            "Cancellation reason is required.",
            new Dictionary<string, string[]> { ["reason"] = ["A cancellation reason is required."] });
    }

    await using var transaction = await dbContext.Database.BeginTransactionAsync(
        System.Data.IsolationLevel.Serializable, cancellationToken);

    var sale = await dbContext.Sales
        .Include(x => x.Items)
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    if (sale is null)
    {
        throw new NotFoundException("Sale not found.");
    }

    if (sale.Status != SaleStatus.Completed)
    {
        throw new ValidationAppException(
            "Sale cannot be cancelled.",
            new Dictionary<string, string[]> { ["status"] = ["Only completed sales can be cancelled."] });
    }

    var productIds = sale.Items
        .Where(x => x.ProductId is not null)
        .Select(x => x.ProductId!.Value)
        .Distinct()
        .ToArray();
    var productsById = await dbContext.Products
        .Where(x => productIds.Contains(x.Id))
        .ToDictionaryAsync(x => x.Id, cancellationToken);

    if (productsById.Count != productIds.Length)
    {
        throw new NotFoundException("A product referenced by the sale no longer exists.");
    }

    foreach (var item in sale.Items.Where(x => x.ProductId is not null))
    {
        var product = productsById[item.ProductId!.Value];
        product.CurrentStock += item.Quantity;
        dbContext.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid(),
            TenantId = sale.TenantId,
            ProductId = product.Id,
            MovementType = InventoryMovementType.Return,
            Quantity = item.Quantity,
            UnitCost = item.UnitCostAtSale,
            ReferenceType = "SaleCancellation",
            ReferenceId = sale.Id,
            Notes = request.Reason.Trim(),
            CreatedByUserId = currentUser.UserId.Value,
            CreatedAt = clock.UtcNow
        });
    }

    sale.Status = SaleStatus.Cancelled;
    sale.CancelledAt = clock.UtcNow;
    sale.CancelledByUserId = currentUser.UserId.Value;
    sale.CancellationReason = request.Reason.Trim();
    var notificationIds = await notificationService.CreateForOwnersAsync(
        sale.TenantId,
        NotificationType.SaleCancelled,
        "Venta cancelada",
        $"Venta #{sale.SequentialNumber} cancelada. Motivo: {sale.CancellationReason}",
        "Sale",
        sale.Id,
        sale.CancelledAt.Value,
        null,
        cancellationToken);

    await dbContext.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    if (services.GetService<IBackgroundJobClient>() is { } notificationJobs)
    {
        foreach (var notificationId in notificationIds)
        {
            notificationJobs.Enqueue<NotificationJobs>(job => job.DeliverAsync(notificationId));
        }
    }

    return Results.Ok(new SaleResponse(sale.Id, sale.SequentialNumber, sale.Subtotal,
        sale.Discount, sale.Total, sale.Status, sale.Items.Count));
}).RequireAuthorization("AdministratorOrOwner");

var products = app.MapGroup("/api/products").RequireAuthorization("CashierOrAbove").WithTags("Products");

products.MapGet("", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var result = await dbContext.Products.AsNoTracking()
        .Where(x => x.IsActive)
        .OrderBy(x => x.Name)
        .Select(x => new ProductResponse(
            x.Id, x.Name, x.Sku, x.CategoryId, x.SalePrice, x.AverageCost,
            x.CurrentStock, x.MinimumStock, x.CriticalStock, x.IsActive))
        .ToListAsync(cancellationToken);

    return Results.Ok(result);
});

products.MapPost("", async (
    CreateProductRequest request,
    AppDbContext dbContext,
    ICurrentTenant currentTenant,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || request.UnitCost < 0 || request.SalePrice < 0 || decimal.Truncate(request.UnitCost) != request.UnitCost || decimal.Truncate(request.SalePrice) != request.SalePrice ||
        request.MinimumStock < 0 || request.CriticalStock < 0 || decimal.Truncate(request.MinimumStock) != request.MinimumStock || decimal.Truncate(request.CriticalStock) != request.CriticalStock ||
        request.CriticalStock > request.MinimumStock)
    {
        throw new ValidationAppException(
            "Invalid product data.",
            new Dictionary<string, string[]>
            {
                ["product"] = ["Name is required, prices and thresholds must be non-negative, and CriticalStock cannot exceed MinimumStock."]
            });
    }

    if (currentTenant.TenantId is null)
    {
        throw new ForbiddenException("A tenant is required.");
    }

    if (request.CategoryId is not null &&
        !await dbContext.ProductCategories.AnyAsync(x => x.Id == request.CategoryId && x.IsActive, cancellationToken))
    {
        throw new NotFoundException("Category not found.");
    }

    var normalizedSku = string.IsNullOrWhiteSpace(request.Sku) ? null : request.Sku.Trim();
    if (normalizedSku is not null &&
        await dbContext.Products.AnyAsync(x => x.Sku == normalizedSku, cancellationToken))
    {
        throw new ValidationAppException(
            "A product with the same SKU already exists.",
            new Dictionary<string, string[]> { ["sku"] = ["SKU must be unique within the tenant."] });
    }

    var product = new Product
    {
        Id = Guid.NewGuid(),
        TenantId = currentTenant.TenantId.Value,
        Name = request.Name.Trim(),
        Sku = normalizedSku,
        CategoryId = request.CategoryId,
        SalePrice = request.SalePrice,
        MinimumStock = request.MinimumStock,
        CriticalStock = request.CriticalStock,
        AverageCost = request.UnitCost,
        CurrentStock = 0,
        IsActive = true
    };

    dbContext.Products.Add(product);
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/products/{product.Id}", new ProductResponse(
        product.Id, product.Name, product.Sku, product.CategoryId, product.SalePrice,
        product.AverageCost, product.CurrentStock, product.MinimumStock,
        product.CriticalStock, product.IsActive));
}).RequireAuthorization("AdministratorOrOwner");

products.MapPatch("/{id:guid}", async (
    Guid id,
    UpdateProductRequest request,
    AppDbContext dbContext,
    ICurrentTenant currentTenant,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || request.UnitCost < 0 || request.SalePrice < 0 || decimal.Truncate(request.UnitCost) != request.UnitCost || decimal.Truncate(request.SalePrice) != request.SalePrice ||
        request.MinimumStock < 0 || request.CriticalStock < 0 || decimal.Truncate(request.MinimumStock) != request.MinimumStock || decimal.Truncate(request.CriticalStock) != request.CriticalStock ||
        request.CriticalStock > request.MinimumStock)
    {
        throw new ValidationAppException(
            "Los datos del producto no son vÃƒÂ¡lidos.",
            new Dictionary<string, string[]> { ["product"] = ["El nombre es obligatorio; los precios y umbrales no pueden ser negativos, y el stock crÃƒÂ­tico no puede superar el stock mÃƒÂ­nimo."] });
    }
    if (currentTenant.TenantId is null) throw new ForbiddenException("Se requiere un tenant.");

    var product = await dbContext.Products.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, cancellationToken);
    if (product is null) throw new NotFoundException("Producto no encontrado.");
    var normalizedSku = string.IsNullOrWhiteSpace(request.Sku) ? null : request.Sku.Trim();
    if (normalizedSku is not null && await dbContext.Products.AnyAsync(x => x.Id != id && x.Sku == normalizedSku, cancellationToken))
        throw new ValidationAppException("El SKU ya estÃƒÂ¡ registrado.", new Dictionary<string, string[]> { ["sku"] = ["El SKU debe ser ÃƒÂºnico dentro del negocio."] });

    product.Name = request.Name.Trim();
    product.Sku = normalizedSku;
    product.AverageCost = request.UnitCost;
    product.SalePrice = request.SalePrice;
    product.MinimumStock = request.MinimumStock;
    product.CriticalStock = request.CriticalStock;
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(new ProductResponse(product.Id, product.Name, product.Sku, product.CategoryId, product.SalePrice, product.AverageCost, product.CurrentStock, product.MinimumStock, product.CriticalStock, product.IsActive));
}).RequireAuthorization("AdministratorOrOwner");
products.MapPost("/{id:guid}/deactivate", async (
    Guid id,
    AppDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var product = await dbContext.Products.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (product is null)
    {
        throw new NotFoundException("Product not found.");
    }

    product.IsActive = false;
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
}).RequireAuthorization("AdministratorOrOwner");

var inventory = app.MapGroup("/api/inventory").RequireAuthorization("AdministratorOrOwner").WithTags("Inventory");

inventory.MapPost("/{productId:guid}/adjust", async (
    Guid productId,
    AdjustInventoryRequest request,
    AppDbContext dbContext,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IClock clock,
    IServiceProvider services,
    CancellationToken cancellationToken) =>
{
    if (request.Quantity == 0 || decimal.Truncate(request.Quantity) != request.Quantity || request.MovementType is not (InventoryMovementType.Adjustment or InventoryMovementType.InitialStock))
    {
        throw new ValidationAppException(
            "Invalid inventory adjustment.",
            new Dictionary<string, string[]> { ["quantity"] = ["La cantidad debe ser un entero distinto de cero y el tipo debe ser Adjustment o InitialStock."] });
    }

    if (currentTenant.TenantId is null || currentUser.UserId is null)
    {
        throw new ForbiddenException("An authenticated tenant user is required.");
    }

    await using var transaction = await dbContext.Database.BeginTransactionAsync(
        System.Data.IsolationLevel.Serializable, cancellationToken);

    var product = await dbContext.Products.FirstOrDefaultAsync(
        x => x.Id == productId && x.IsActive, cancellationToken);
    if (product is null)
    {
        throw new NotFoundException("Product not found.");
    }

    var nextStock = product.CurrentStock + request.Quantity;
    if (nextStock < 0)
    {
        throw new ValidationAppException(
            "Inventory cannot become negative.",
            new Dictionary<string, string[]> { ["quantity"] = ["The adjustment would result in negative stock."] });
    }

    product.CurrentStock = nextStock;
    var movement = new InventoryMovement
    {
        Id = Guid.NewGuid(),
        TenantId = currentTenant.TenantId.Value,
        ProductId = product.Id,
        MovementType = request.MovementType,
        Quantity = request.Quantity,
        UnitCost = request.UnitCost,
        Notes = request.Notes?.Trim(),
        CreatedByUserId = currentUser.UserId.Value,
        CreatedAt = clock.UtcNow
    };

    dbContext.InventoryMovements.Add(movement);
    await dbContext.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    services.GetService<IBackgroundJobClient>()?.Enqueue<InventoryAlertJobs>(job => job.CheckAsync(CancellationToken.None));

    return Results.Ok(new
    {
        product.Id,
        product.CurrentStock,
        MovementId = movement.Id
    });
});

inventory.MapGet("/movements", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var movements = await dbContext.InventoryMovements.AsNoTracking()
        .OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(cancellationToken);
    var productIds = movements.Select(x => x.ProductId).Distinct().ToArray();
    var names = await dbContext.Products.AsNoTracking().Where(x => productIds.Contains(x.Id))
        .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
    return Results.Ok(movements.Select(x => new
    {
        x.Id,
        x.ProductId,
        ProductName = names.GetValueOrDefault(x.ProductId, "Producto"),
        x.MovementType,
        x.Quantity,
        x.UnitCost,
        x.Notes,
        x.CreatedAt
    }));
});
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

if (!app.Environment.IsEnvironment("Testing"))
{
    var bogotaTimeZone = OperatingSystem.IsWindows()
        ? TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time")
        : TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
    var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();
    recurringJobManager.AddOrUpdate<PdfGenerationJobs>("reports-daily", job => job.GenerateDailyReportsAsync(), Cron.Daily(2), new RecurringJobOptions { TimeZone = bogotaTimeZone });
    recurringJobManager.AddOrUpdate<PdfGenerationJobs>("reports-monthly", job => job.GenerateMonthlyReportsAsync(), Cron.Monthly(1, 3), new RecurringJobOptions { TimeZone = bogotaTimeZone });
    recurringJobManager.AddOrUpdate<PdfGenerationJobs>("pdf-retry-failed", job => job.RetryFailedPdfJobsAsync(), Cron.Hourly(), new RecurringJobOptions { TimeZone = bogotaTimeZone });
    recurringJobManager.AddOrUpdate<InventoryAlertJobs>("inventory-alerts", job => job.CheckAsync(CancellationToken.None), Cron.MinuteInterval(5), new RecurringJobOptions { TimeZone = bogotaTimeZone });
}

app.Run();

public partial class Program;
































