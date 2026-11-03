using BillaresPos.Domain.Identity;
using BillaresPos.Domain.Tenancy;
using BillaresPos.Infrastructure.Identity;
using BillaresPos.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BillaresPos.IntegrationTests;

public sealed class TestAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public Guid TenantAId { get; private set; }
    public Guid TenantBId { get; private set; }
    public Guid OwnerAId { get; private set; }
    public Guid OwnerBId { get; private set; }
    public Guid ProductAId { get; private set; }
    public Guid ProductBId { get; private set; }
    public Guid TableBId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused",
                ["Database:Provider"] = "None",
                ["Jwt:Issuer"] = "BillaresPos",
                ["Jwt:Audience"] = "BillaresPos.Api",
                ["Jwt:SigningKey"] = "development-only-change-me-please-32-chars-minimum",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "14"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await SeedAsync(scope.ServiceProvider);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    private async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var tenantA = new Tenant { Id = Guid.NewGuid(), Name = "Tenant A", Slug = "tenant-a" };
        var tenantB = new Tenant { Id = Guid.NewGuid(), Name = "Tenant B", Slug = "tenant-b" };
        db.Tenants.AddRange(tenantA, tenantB);
        await db.SaveChangesAsync();

        var ownerA = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantA.Id,
            Email = "owner@tenant-a.local",
            UserName = "owner@tenant-a.local",
            FullName = "Owner A",
            EmailConfirmed = true,
            IsActive = true
        };
        var ownerB = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantB.Id,
            Email = "owner@tenant-b.local",
            UserName = "owner@tenant-b.local",
            FullName = "Owner B",
            EmailConfirmed = true,
            IsActive = true
        };

        await userManager.CreateAsync(ownerA, "TenantA123!");
        await userManager.CreateAsync(ownerB, "TenantB123!");
        await userManager.AddToRoleAsync(ownerA, RoleNames.Owner);
        await userManager.AddToRoleAsync(ownerB, RoleNames.Owner);

        TenantAId = tenantA.Id;
        TenantBId = tenantB.Id;
        OwnerAId = ownerA.Id;
        OwnerBId = ownerB.Id;

        var productA = new BillaresPos.Domain.Products.Product
        {
            Id = Guid.NewGuid(), TenantId = tenantA.Id, Name = "Poker", SalePrice = 5000,
            MinimumStock = 10, CriticalStock = 3, IsActive = true
        };
        var productB = new BillaresPos.Domain.Products.Product
        {
            Id = Guid.NewGuid(), TenantId = tenantB.Id, Name = "Poker", SalePrice = 5000,
            MinimumStock = 10, CriticalStock = 3, IsActive = true
        };
        db.Products.AddRange(productA, productB);
        await db.SaveChangesAsync();
        ProductAId = productA.Id;
        ProductBId = productB.Id;

        var tableB = new BillaresPos.Domain.Billiards.BilliardTable
        {
            Id = Guid.NewGuid(), TenantId = tenantB.Id, Name = "Mesa B", HourlyRate = 6000,
            BillingIncrementMinutes = 15, Status = BillaresPos.Domain.Billiards.BilliardTableStatus.Available, IsActive = true
        };
        db.BilliardTables.Add(tableB);
        await db.SaveChangesAsync();
        TableBId = tableB.Id;
    }
}







