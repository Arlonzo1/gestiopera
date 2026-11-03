using System.Text.Json;
using BillaresPos.Application.Common.Abstractions;
using BillaresPos.Domain.Auditing;
using BillaresPos.Domain.Common;
using BillaresPos.Domain.Tenancy;
using BillaresPos.Infrastructure.Identity;
using BillaresPos.Domain.Inventory;
using BillaresPos.Domain.Products;
using BillaresPos.Domain.Purchases;
using BillaresPos.Domain.Sales;
using BillaresPos.Domain.Billiards;
using BillaresPos.Domain.CashManagement;
using BillaresPos.Domain.Reports;
using BillaresPos.Domain.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BillaresPos.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IClock clock) : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<PhysicalInventoryCount> PhysicalInventoryCounts => Set<PhysicalInventoryCount>();
    public DbSet<PhysicalInventoryCountItem> PhysicalInventoryCountItems => Set<PhysicalInventoryCountItem>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SalePayment> SalePayments => Set<SalePayment>();
    public DbSet<BilliardTable> BilliardTables => Set<BilliardTable>();
    public DbSet<BilliardSession> BilliardSessions => Set<BilliardSession>();
    public DbSet<BilliardConsumption> BilliardConsumptions => Set<BilliardConsumption>();
    public DbSet<CashShift> CashShifts => Set<CashShift>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<GeneratedDocument> GeneratedDocuments => Set<GeneratedDocument>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<PushDeviceToken> PushDeviceTokens => Set<PushDeviceToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("app");

        builder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(80).IsRequired();
            entity.Property(x => x.TimeZoneId).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.LogoDataUrl).HasMaxLength(1000000);
            entity.HasIndex(x => x.Slug).IsUnique();
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(80).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(80).IsRequired();
            entity.Property(x => x.OldValuesJson).HasColumnType("jsonb");
            entity.Property(x => x.NewValuesJson).HasColumnType("jsonb");
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.HasIndex(x => new { x.TenantId, x.CreatedAt });
            entity.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => currentTenant.TenantId != null && x.TenantId == currentTenant.TenantId);
        });
        builder.Entity<GeneratedDocument>(entity =>
        {
            entity.ToTable("generated_documents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DocumentType).HasMaxLength(60).IsRequired();
            entity.Property(x => x.FileName).HasMaxLength(180).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Content).HasColumnType("bytea");
            entity.Property(x => x.Error).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantId, x.DocumentType, x.ReferenceId });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => currentTenant.TenantId != null && x.TenantId == currentTenant.TenantId);
        });

        builder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Channel).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.ReferenceType).HasMaxLength(80);
            entity.Property(x => x.ExternalMessageId).HasMaxLength(200);
            entity.Property(x => x.Error).HasMaxLength(2000);
            entity.HasIndex(x => new { x.TenantId, x.RecipientUserId, x.Status, x.CreatedAt });
            entity.HasIndex(x => new { x.TenantId, x.Type, x.ReferenceType, x.ReferenceId });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => currentTenant.TenantId != null && x.TenantId == currentTenant.TenantId);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("users");
            entity.Property(x => x.FullName).HasMaxLength(160).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.NormalizedEmail }).IsUnique();
            entity.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => currentTenant.TenantId != null && x.TenantId == currentTenant.TenantId);
        });

        builder.Entity<IdentityRole<Guid>>(entity => entity.ToTable("roles"));
        builder.Entity<IdentityUserRole<Guid>>(entity => entity.ToTable("user_roles"));
        builder.Entity<IdentityUserClaim<Guid>>(entity => entity.ToTable("user_claims"));
        builder.Entity<IdentityUserLogin<Guid>>(entity => entity.ToTable("user_logins"));
        builder.Entity<IdentityRoleClaim<Guid>>(entity => entity.ToTable("role_claims"));
        builder.Entity<IdentityUserToken<Guid>>(entity => entity.ToTable("user_tokens"));

        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.UserId });
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(x => currentTenant.TenantId != null && x.TenantId == currentTenant.TenantId);
        });
        builder.Entity<ProductCategory>(entity =>
        {
            entity.ToTable("product_categories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => currentTenant.TenantId != null && x.TenantId == currentTenant.TenantId);
        });

        builder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Sku).HasMaxLength(80);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.SalePrice).HasPrecision(18, 2);
            entity.Property(x => x.AverageCost).HasPrecision(18, 2);
            entity.Property(x => x.CurrentStock).HasPrecision(18, 3);
            entity.Property(x => x.MinimumStock).HasPrecision(18, 3);
            entity.Property(x => x.CriticalStock).HasPrecision(18, 3);
            entity.HasIndex(x => new { x.TenantId, x.Sku }).IsUnique().HasFilter("\"Sku\" IS NOT NULL");
            entity.HasIndex(x => new { x.TenantId, x.Name });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProductCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(x => currentTenant.TenantId != null && x.TenantId == currentTenant.TenantId);
        });

        builder.Entity<InventoryMovement>(entity =>
        {
            entity.ToTable("inventory_movements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MovementType).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.Property(x => x.ReferenceType).HasMaxLength(80);
            entity.Property(x => x.Notes).HasMaxLength(500);

            entity.HasIndex(x => new { x.TenantId, x.ProductId, x.CreatedAt });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => currentTenant.TenantId != null && x.TenantId == currentTenant.TenantId);
        });


        builder.Entity<PhysicalInventoryCount>(entity =>
        {
            entity.ToTable("physical_inventory_counts");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.CountedAt });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => currentTenant.TenantId != null && x.TenantId == currentTenant.TenantId);
        });

        builder.Entity<PhysicalInventoryCountItem>(entity =>
        {
            entity.ToTable("physical_inventory_count_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SystemQuantity).HasPrecision(18, 3);
            entity.Property(x => x.PhysicalQuantity).HasPrecision(18, 3);
            entity.Property(x => x.Difference).HasPrecision(18, 3);
            entity.HasIndex(x => new { x.TenantId, x.ProductId });
            entity.HasOne(x => x.Count).WithMany(x => x.Items).HasForeignKey(x => x.PhysicalInventoryCountId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => currentTenant.TenantId != null && x.TenantId == currentTenant.TenantId);
        });
        builder.Entity<Purchase>(entity =>
        {
            entity.ToTable("purchases");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Supplier).HasMaxLength(160);
            entity.Property(x => x.InvoiceReference).HasMaxLength(120);
            entity.Property(x => x.Total).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.PurchaseDate });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => currentTenant.TenantId != null && x.TenantId == currentTenant.TenantId);
        });

        builder.Entity<PurchaseItem>(entity =>
        {
            entity.ToTable("purchase_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitCost).HasPrecision(18, 2);
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.ProductId });
            entity.HasOne(x => x.Purchase).WithMany(x => x.Items).HasForeignKey(x => x.PurchaseId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => currentTenant.TenantId != null && x.TenantId == currentTenant.TenantId);
        });

        builder.Entity<Sale>(entity =>
        {
            entity.ToTable("sales");
            entity.Property(x => x.CustomerName).HasMaxLength(160);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.Property(x => x.Discount).HasPrecision(18, 2);
            entity.Property(x => x.Total).HasPrecision(18, 2);
            entity.Property(x => x.Notes).HasMaxLength(500);

            entity.Property(x => x.CancellationReason).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.SequentialNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.CreatedAt });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => currentTenant.TenantId != null && x.TenantId == currentTenant.TenantId);
        });

        builder.Entity<SaleItem>(entity =>
        {
            entity.ToTable("sale_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DescriptionSnapshot).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.UnitCostAtSale).HasPrecision(18, 2);
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.Property(x => x.ItemType).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.ProductId });
            entity.HasOne(x => x.Sale).WithMany(x => x.Items).HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => currentTenant.TenantId != null && x.TenantId == currentTenant.TenantId);
        });

        builder.Entity<SalePayment>(entity =>
        {
            entity.ToTable("sale_payments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Method).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.SaleId });
            entity.HasOne(x => x.Sale).WithMany(x => x.Payments).HasForeignKey(x => x.SaleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => currentTenant.TenantId != null && x.TenantId == currentTenant.TenantId);
        });

        builder.Entity<BilliardTable>(entity =>
        {
            entity.ToTable("billiard_tables");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.Property(x => x.HourlyRate).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => currentTenant.TenantId != null && x.TenantId == currentTenant.TenantId);
        });

        builder.Entity<BilliardSession>(entity =>
        {
            entity.ToTable("billiard_sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.HourlyRateSnapshot).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.BilliardTableId, x.Status });
            entity.HasOne<BilliardTable>().WithMany().HasForeignKey(x => x.BilliardTableId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => currentTenant.TenantId != null && x.TenantId == currentTenant.TenantId);
        });

        builder.Entity<CashShift>(entity =>
        {
            entity.ToTable("cash_shifts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.OpeningCash).HasPrecision(18, 2);
            entity.Property(x => x.ExpectedCash).HasPrecision(18, 2);
            entity.Property(x => x.CountedCash).HasPrecision(18, 2);
            entity.Property(x => x.Difference).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.UserId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.OpenedAt });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => currentTenant.TenantId != null && x.TenantId == currentTenant.TenantId);
        });

        builder.Entity<Expense>(entity =>
        {
            entity.ToTable("expenses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(30).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.HasIndex(x => new { x.TenantId, x.CreatedAt });
            entity.HasIndex(x => new { x.TenantId, x.CashShiftId });
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CashShift>().WithMany().HasForeignKey(x => x.CashShiftId).OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(x => currentTenant.TenantId != null && x.TenantId == currentTenant.TenantId);
        });
        builder.Entity<BilliardConsumption>(entity =>
        {
            entity.ToTable("billiard_consumptions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DescriptionSnapshot).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.UnitCostAtConsumption).HasPrecision(18, 2);
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.TenantId, x.BilliardSessionId });
            entity.HasOne(x => x.BilliardSession).WithMany(x => x.Consumptions).HasForeignKey(x => x.BilliardSessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => currentTenant.TenantId != null && x.TenantId == currentTenant.TenantId);
        });

    }
    private void AddAutomaticAuditLogs(DateTimeOffset now)
    {
        if (currentTenant.TenantId is null || currentUser.UserId is null)
            return;

        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified).ToList())
        {
            var action = ResolveAuditAction(entry);
            if (action is null || entry.Entity is AuditLog)
                continue;

            if (entry.Entity is not ITenantOwned tenantOwned || tenantOwned.TenantId != currentTenant.TenantId.Value)
                continue;

            var oldValues = entry.State == EntityState.Modified
                ? entry.Properties.Where(x => x.IsModified).ToDictionary(x => x.Metadata.Name, x => x.OriginalValue)
                : null;
            var newValues = entry.Properties.Where(x => entry.State == EntityState.Added || x.IsModified)
                .ToDictionary(x => x.Metadata.Name, x => x.CurrentValue);
            var entityId = entry.Property("Id").CurrentValue is Guid id ? id : (Guid?)null;

            AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = currentTenant.TenantId.Value,
                UserId = currentUser.UserId.Value,
                Action = action,
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = entityId,
                OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
                NewValuesJson = JsonSerializer.Serialize(newValues),
                CreatedAt = now
            });
        }
    }

    private static string? ResolveAuditAction(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var type = entry.Metadata.ClrType;
        if (type == typeof(Sale))
        {
            if (entry.State == EntityState.Added) return "SALE_CREATED";
            if (entry.Property(nameof(Sale.Status)).IsModified && entry.Property(nameof(Sale.Status)).CurrentValue is SaleStatus.Cancelled)
                return "SALE_CANCELLED";
        }
        if (type == typeof(Purchase) && entry.State == EntityState.Added) return "PURCHASE_CREATED";
        if (type == typeof(InventoryMovement) && entry.State == EntityState.Added) return "INVENTORY_ADJUSTED";
        if (type == typeof(Product) && entry.State == EntityState.Modified && entry.Property(nameof(Product.SalePrice)).IsModified)
            return "PRODUCT_PRICE_CHANGED";
        if (type == typeof(Expense) && entry.State == EntityState.Added) return "EXPENSE_CREATED";
        if (type == typeof(CashShift))
        {
            if (entry.State == EntityState.Added) return "CASH_SHIFT_OPENED";
            if (entry.Property(nameof(CashShift.Status)).IsModified && entry.Property(nameof(CashShift.Status)).CurrentValue is CashShiftStatus.Closed)
                return "CASH_SHIFT_CLOSED";
        }
        if (type == typeof(BilliardSession))
        {
            if (entry.State == EntityState.Added) return "BILLIARD_SESSION_STARTED";
            if (entry.Property(nameof(BilliardSession.Status)).IsModified && entry.Property(nameof(BilliardSession.Status)).CurrentValue is BilliardSessionStatus.Closed)
                return "BILLIARD_SESSION_CLOSED";
        }
        return null;
    }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        foreach (var entry in ChangeTracker.Entries<Tenant>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ApplicationUser>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }


        foreach (var entry in ChangeTracker.Entries<ProductCategory>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<Product>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<InventoryMovement>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
            {
                entry.Entity.CreatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<Expense>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default) entry.Entity.CreatedAt = now;
        }

        AddAutomaticAuditLogs(now);
        foreach (var entry in ChangeTracker.Entries<Purchase>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
















