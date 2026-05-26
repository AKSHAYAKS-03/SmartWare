using System;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.Entities;

namespace SmartInventory.Repository;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    #region DbSets
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseZone> WarehouseZones => Set<WarehouseZone>();
    public DbSet<BinLocation> BinLocations => Set<BinLocation>();
    public DbSet<UserWarehouseAccess> UserWarehouseAccesses => Set<UserWarehouseAccess>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<AlertConfiguration> AlertConfigurations => Set<AlertConfiguration>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierProduct> SupplierProducts => Set<SupplierProduct>();
    public DbSet<SupplierPerformanceLog> SupplierPerformanceLogs => Set<SupplierPerformanceLog>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptItem> GoodsReceiptItems => Set<GoodsReceiptItem>();
    public DbSet<Barcode> Barcodes => Set<Barcode>();
    public DbSet<BarcodeScanLog> BarcodeScanLogs => Set<BarcodeScanLog>();
    public DbSet<WarehouseTransfer> WarehouseTransfers => Set<WarehouseTransfer>();
    public DbSet<TransferItem> TransferItems => Set<TransferItem>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<FileAttachment> FileAttachments => Set<FileAttachment>();
    public DbSet<SequenceCounter> SequenceCounters => Set<SequenceCounter>();
    #endregion

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from the repository assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Seed data
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // 1. Roles
        var adminRoleId = new Guid("a0d33b91-4567-4eef-b123-999999999901");
        var managerRoleId = new Guid("a0d33b91-4567-4eef-b123-999999999902");
        var staffRoleId = new Guid("a0d33b91-4567-4eef-b123-999999999903");
        var viewerRoleId = new Guid("a0d33b91-4567-4eef-b123-999999999904");

        modelBuilder.Entity<Role>().HasData(
            new Role { Id = adminRoleId, Name = "Admin", Description = "Full system access with administrative rights.", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = managerRoleId, Name = "Manager", Description = "Warehouse and inventory management level access.", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = staffRoleId, Name = "Staff", Description = "Day-to-day warehouse operations access.", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = viewerRoleId, Name = "Viewer", Description = "Read-only access to catalogs and reports.", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        // 2. Default Admin User (Password is pre-hashed: Admin@123)
        // Hashed using BCrypt.Net: $2a$11$W2.D1u5q0vF9lWlJpM6z1eZ6gCshK/2mZ/9fL1Z3O9fGqJ2Q5k4z2
        var adminUserId = new Guid("b0d33b91-4567-4eef-b123-888888888801");
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = adminUserId,
                FullName = "System Administrator",
                Email = "admin@smartinventory.com",
                PasswordHash = "$2a$11$W2.D1u5q0vF9lWlJpM6z1eZ6gCshK/2mZ/9fL1Z3O9fGqJ2Q5k4z2",
                PhoneNumber = "+15550199",
                SmsEnabled = false,
                EmailEnabled = true,
                IsActive = true,
                RoleId = adminRoleId,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // 3. Sequence Counters
        modelBuilder.Entity<SequenceCounter>().HasData(
            new SequenceCounter { Id = new Guid("c0d33b91-4567-4eef-b123-777777777701"), EntityName = "PurchaseOrder", Prefix = "PO", CurrentValue = 0, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new SequenceCounter { Id = new Guid("c0d33b91-4567-4eef-b123-777777777702"), EntityName = "GoodsReceipt", Prefix = "GRN", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new SequenceCounter { Id = new Guid("c0d33b91-4567-4eef-b123-777777777703"), EntityName = "WarehouseTransfer", Prefix = "TRF", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new SequenceCounter { Id = new Guid("c0d33b91-4567-4eef-b123-777777777704"), EntityName = "StockAdjustment", Prefix = "ADJ", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new SequenceCounter { Id = new Guid("c0d33b91-4567-4eef-b123-777777777705"), EntityName = "Product", Prefix = "PRD", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new SequenceCounter { Id = new Guid("c0d33b91-4567-4eef-b123-777777777706"), EntityName = "Supplier", Prefix = "SUP", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new SequenceCounter { Id = new Guid("c0d33b91-4567-4eef-b123-777777777707"), EntityName = "Warehouse", Prefix = "WH", CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
