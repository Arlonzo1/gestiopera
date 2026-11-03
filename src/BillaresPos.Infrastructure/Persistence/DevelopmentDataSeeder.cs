using BillaresPos.Domain.Identity;
using BillaresPos.Domain.Billiards;
using BillaresPos.Domain.Tenancy;
using BillaresPos.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BillaresPos.Infrastructure.Persistence;

public sealed class DevelopmentDataSeeder(
    AppDbContext dbContext,
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<ApplicationUser> userManager,
    ILogger<DevelopmentDataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        foreach (var role in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                ThrowIfFailed(result, $"creating role {role}");
            }
        }

        var tenant = await dbContext.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Slug == "billar-demo", cancellationToken);
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = "Billar Demo",
                Slug = "billar-demo",
                TimeZoneId = "America/Bogota",
                Currency = "COP"
            };
            dbContext.Tenants.Add(tenant);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var tableCount = await dbContext.BilliardTables.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenant.Id, cancellationToken);
        if (tableCount == 0)
        {
            dbContext.BilliardTables.AddRange(Enumerable.Range(1, 4).Select(index => new BilliardTable
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = $"Mesa {index}",
                HourlyRate = 20000,
                BillingIncrementMinutes = 15,
                Status = BilliardTableStatus.Available,
                IsActive = true
            }));
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded four billiard tables for tenant {TenantSlug}", tenant.Slug);
        }

        const string email = "owner@billar-demo.local";
        var owner = await dbContext.Users.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.NormalizedEmail == email.ToUpperInvariant(), cancellationToken);
        if (owner is null)
        {
            owner = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Email = email,
                UserName = email,
                FullName = "Demo Owner",
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await userManager.CreateAsync(owner, "DemoOwner123!");
            ThrowIfFailed(result, "creating demo owner");
            result = await userManager.AddToRoleAsync(owner, RoleNames.Owner);
            ThrowIfFailed(result, "assigning owner role");
            logger.LogInformation("Seeded development owner {Email} for tenant {TenantSlug}", email, tenant.Slug);
        }
    }

    private static void ThrowIfFailed(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Failed {operation}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }
}

