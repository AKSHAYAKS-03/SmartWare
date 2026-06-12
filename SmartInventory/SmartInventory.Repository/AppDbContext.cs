using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Interfaces;
using SmartInventory.Core.Enums;

namespace SmartInventory.Repository;

public class AppDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUserService;
    

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService? currentUserService = null) : base(options)
    {
        _currentUserService = currentUserService;
    }

    protected AppDbContext(DbContextOptions options, ICurrentUserService? currentUserService = null) : base(options)
    {
        _currentUserService = currentUserService;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(w => w
            .Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)
            .Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        base.OnConfiguring(optionsBuilder);
    }

    #region DbSets
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OverrideAuditLog> OverrideAuditLogs => Set<OverrideAuditLog>();
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
    public DbSet<PurchaseOrderShipment> PurchaseOrderShipments => Set<PurchaseOrderShipment>();
    public DbSet<PurchaseOrderShipmentItem> PurchaseOrderShipmentItems => Set<PurchaseOrderShipmentItem>();
    public DbSet<Barcode> Barcodes => Set<Barcode>();
    public DbSet<BarcodeScanLog> BarcodeScanLogs => Set<BarcodeScanLog>();
    public DbSet<WarehouseTransfer> WarehouseTransfers => Set<WarehouseTransfer>();
    public DbSet<TransferItem> TransferItems => Set<TransferItem>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<FileAttachment> FileAttachments => Set<FileAttachment>();
    public DbSet<SequenceCounter> SequenceCounters => Set<SequenceCounter>();
    public DbSet<AuditLogArchive> AuditLogArchives => Set<AuditLogArchive>();

    // Supplier Portal
    public DbSet<SupplierContact> SupplierContacts => Set<SupplierContact>();
    public DbSet<SupplierRefreshToken> SupplierRefreshTokens => Set<SupplierRefreshToken>();
    public DbSet<SupplierInvoice> SupplierInvoices => Set<SupplierInvoice>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    #endregion

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from the repository assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        if (Database.IsNpgsql())
        {
            // Native PostgreSQL Sequences
            modelBuilder.HasSequence<int>("seq_purchase_orders").StartsAt(1);
            modelBuilder.HasSequence<int>("seq_goods_receipts").StartsAt(1);
            modelBuilder.HasSequence<int>("seq_transfers").StartsAt(1);
            modelBuilder.HasSequence<int>("seq_adjustments").StartsAt(1);
            modelBuilder.HasSequence<int>("seq_suppliers").StartsAt(1);
            modelBuilder.HasSequence<int>("seq_shipments").StartsAt(1);
            modelBuilder.HasSequence<int>("seq_products").StartsAt(1);
            modelBuilder.HasSequence<int>("seq_warehouses").StartsAt(1);
            modelBuilder.HasSequence<int>("seq_zones").StartsAt(1);
            modelBuilder.HasSequence<int>("seq_bins").StartsAt(1);
            modelBuilder.HasSequence<int>("seq_invoices").StartsAt(1);
            modelBuilder.HasSequence<int>("seq_tracking_numbers").StartsAt(1);

            // Register PostgreSQL Trigram extension for advanced LIKE/contains searching
            modelBuilder.HasPostgresExtension("pg_trgm");
        }

        // Indexes
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<PurchaseOrder>().HasIndex(p => p.PoNumber).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(p => p.SKU).IsUnique();
        modelBuilder.Entity<RefreshToken>().HasIndex(r => r.Token).IsUnique();
        modelBuilder.Entity<Supplier>().HasIndex(s => s.GSTIN).IsUnique();
        modelBuilder.Entity<Supplier>().HasIndex(s => s.PAN).IsUnique();

        if (Database.IsNpgsql())
        {
            // PostgreSQL GIN Indexes for fast substring (contains) searches
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.Name)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");

            modelBuilder.Entity<PurchaseOrder>()
                .HasIndex(p => p.Notes)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");

            modelBuilder.Entity<WarehouseTransfer>()
                .HasIndex(t => t.Notes)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");
        }

        // Optimistic Concurrency Control (Lost Updates Prevention) using PostgreSQL xmin
        if (Database.IsNpgsql())
        {
            modelBuilder.Entity<StockLevel>().Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<PurchaseOrder>().Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<StockAdjustment>().Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<WarehouseTransfer>().Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            modelBuilder.Entity<BinLocation>().Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        }

        // Global Query Filters for Soft Deletes Filtering Setup:
        // Automatically injects a database filter (IsActive == true) to every entity implementing ISoftDelete.
        // This ensures soft deleted records are filtered out at the DB level, unless explicitly bypassed using .IgnoreQueryFilters()
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, nameof(ISoftDelete.IsActive));
                var constant = Expression.Constant(true);
                var body = Expression.Equal(property, constant);
                var lambda = Expression.Lambda(body, parameter);

                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);

                // Add Index for IsActive to prevent full table scans
                modelBuilder.Entity(entityType.ClrType).HasIndex(nameof(ISoftDelete.IsActive));
            }
        }

        // Seed data
        SeedData(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Convert actual Hard Deletes to Soft Deletes for ISoftDelete entities
        HandleSoftDelete();

        //  Update UpdatedAt for BaseEntity
        HandleUpdatedAt();

        // Create Outbox messages for StockLevel changes
        HandleStockLevelOutbox();

        // Capture entity state changes in-memory before saving database modifications
        var auditEntries = OnBeforeSaveChanges();

        var hasOutboxMessages = ChangeTracker.Entries<OutboxMessage>().Any(e => e.State == EntityState.Added);

        // Save actual changes to the database
        var result = await base.SaveChangesAsync(cancellationToken);

        if (hasOutboxMessages && Database.IsNpgsql())
        {
            await Database.ExecuteSqlRawAsync("NOTIFY outbox_ready;", cancellationToken);
        }

        // Save audit logs (if any modifications occurred)
        if (auditEntries.Any())
        {
            await SaveAuditLogsAsync(auditEntries, cancellationToken);
        }

        return result;
    }


    private void HandleSoftDelete()
    {
        foreach (var entry in ChangeTracker.Entries<ISoftDelete>())
        {
            if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsActive = false;
            }
        }
    }

    /// <summary>
    /// Walk the change tracker to set UpdatedAt.
    /// </summary>
    private void HandleUpdatedAt()
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    private void HandleStockLevelOutbox()
    {
        var outboxMessages = new List<OutboxMessage>();
        foreach (var entry in ChangeTracker.Entries<StockLevel>())
        {
            if (entry.State == EntityState.Modified || entry.State == EntityState.Added)
            {
                var sl = entry.Entity;
                var payload = new { ProductId = sl.ProductId, WarehouseId = sl.WarehouseId, QuantityOnHand = sl.QuantityOnHand };
                outboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = "StockLevelChanged",
                    Payload = JsonSerializer.Serialize(payload),
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        
        if (outboxMessages.Any())
        {
            OutboxMessages.AddRange(outboxMessages);
        }
    }

    /// <summary>
    /// Intercept tracked changes, preparing an audit record for every Create, Update, and Delete operation.
    /// </summary>
    private List<AuditEntry> OnBeforeSaveChanges()
    {
        ChangeTracker.DetectChanges();
        var auditEntries = new List<AuditEntry>();
        
        // Use ICurrentUserService context. Fallback to null if executed anonymously or out of request scope.
        var currentUserId = _currentUserService?.UserId;
        if (currentUserId == Guid.Empty)
        {
            currentUserId = null;
        }

        foreach (var entry in ChangeTracker.Entries())
        {
            // Never audit logs themselves or unchanged/detached entries
            if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                continue;

            var auditEntry = new AuditEntry(entry)
            {
                TableName = entry.Entity.GetType().Name,
                // If it's an unauthenticated action (like Login), and the entity is a User, set the Actor to that User's ID
                UserId = currentUserId ?? (entry.Entity is User userEntity ? userEntity.Id : null),
                IpAddress = _currentUserService?.IpAddress
            };
            auditEntries.Add(auditEntry);

            foreach (var property in entry.Properties)
            {
                string propertyName = property.Metadata.Name;
                if (property.Metadata.IsPrimaryKey())
                {
                    auditEntry.KeyValues[propertyName] = property.CurrentValue;
                    continue;
                }

                switch (entry.State)
                {
                    case EntityState.Added:
                        auditEntry.Action = "Create";
                        auditEntry.NewValues[propertyName] = property.CurrentValue;
                        break;

                    case EntityState.Deleted:
                        auditEntry.Action = "Delete";
                        auditEntry.OldValues[propertyName] = property.OriginalValue;
                        break;

                    case EntityState.Modified:
                        if (property.IsModified)
                        {
                            auditEntry.Action = "Update";
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                        }
                        break;
                }
            }
        }

        return auditEntries;
    }


    private async Task SaveAuditLogsAsync(List<AuditEntry> auditEntries, CancellationToken cancellationToken)
    {
        foreach (var auditEntry in auditEntries)
        {
            AuditLogs.Add(auditEntry.ToAuditLog());
        }
        await base.SaveChangesAsync(cancellationToken);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // 1. Roles
        var adminRoleId = new Guid("a0d33b91-4567-4eef-b123-999999999901");
        var managerRoleId = new Guid("a0d33b91-4567-4eef-b123-999999999902");
        var staffRoleId = new Guid("a0d33b91-4567-4eef-b123-999999999903");
        var viewerRoleId = new Guid("a0d33b91-4567-4eef-b123-999999999904");

        modelBuilder.Entity<Role>().HasData(
            new Role { Id = adminRoleId, Name = "Admin", Description = "Full system access with administrative rights.", Permissions = ["Admin", "Manage", "Inventory", "View"], CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = managerRoleId, Name = "Manager", Description = "Warehouse and inventory management level access.", Permissions = ["Manage", "Inventory", "View"], CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = staffRoleId, Name = "Staff", Description = "Day-to-day warehouse operations access.", Permissions = ["Inventory", "View"], CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = viewerRoleId, Name = "Viewer", Description = "Read-only access to catalogs and reports.", Permissions = ["View"], CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );

        // 2. Default Admin User (Password is pre-hashed: Admin@123)
        // Hashed using BCrypt.Net: $2a$11$PoX6gPtxzAqiQq6Eht0mqOR5Snv/5XxOJA7Bl3P2bH89dAWIw5BD.
        var adminUserId = new Guid("b0d33b91-4567-4eef-b123-888888888801");
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = adminUserId,
                FullName = "System Administrator",
                Email = "smartwareinventory@gmail.com",
                PasswordHash = "$2a$11$PoX6gPtxzAqiQq6Eht0mqOR5Snv/5XxOJA7Bl3P2bH89dAWIw5BD.",
                PhoneNumber = "+15550199",
                SmsEnabled = false,
                EmailEnabled = true,
                IsActive = true,
                Status = UserStatus.Active,
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

// Helper class representing a tracked entity transaction to form a structured JSONB log.
internal class AuditEntry
{
    public AuditEntry(EntityEntry entry)
    {
        Entry = entry;
    }

    public EntityEntry Entry { get; }
    public Guid? UserId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public Dictionary<string, object?> KeyValues { get; } = new();
    public Dictionary<string, object?> OldValues { get; } = new();
    public Dictionary<string, object?> NewValues { get; } = new();

    public AuditLog ToAuditLog()
    {
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            EntityType = TableName,
            EntityId = KeyValues.Count > 0 ? (Guid)KeyValues.Values.First()! : Guid.Empty,
            Action = Action,
            OldValues = OldValues.Count == 0 ? null : JsonSerializer.Serialize(OldValues),
            NewValues = NewValues.Count == 0 ? null : JsonSerializer.Serialize(NewValues),
            IpAddress = IpAddress,
            CreatedAt = DateTime.UtcNow
        };
    }
}
