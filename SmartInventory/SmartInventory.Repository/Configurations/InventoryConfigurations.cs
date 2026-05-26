using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;


namespace SmartInventory.Repository.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Slug)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.Description)
            .HasMaxLength(250);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.HasOne(x => x.Parent)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => x.ParentId);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.SKU)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.UnitOfMeasure)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.CostPrice)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.SellingPrice)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.ReorderPoint)
            .HasDefaultValue(0);

        builder.Property(x => x.ReorderQuantity)
            .HasDefaultValue(10);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.Property(x => x.ImagePath)
            .HasMaxLength(500);

        builder.HasOne(x => x.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SKU).IsUnique();
        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => new { x.CategoryId, x.IsActive });
    }
}

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("product_variants");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.VariantName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.SkuSuffix)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Attributes)
            .HasColumnType("jsonb");

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.HasOne(x => x.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ProductId, x.SkuSuffix }).IsUnique();
    }
}

public class StockLevelConfiguration : IEntityTypeConfiguration<StockLevel>
{
    public void Configure(EntityTypeBuilder<StockLevel> builder)
    {
        builder.ToTable("stock_levels");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.QuantityOnHand)
            .HasDefaultValue(0);

        builder.Property(x => x.QuantityReserved)
            .HasDefaultValue(0);

        builder.Property(x => x.QuantityOnOrder)
            .HasDefaultValue(0);

        builder.Property(x => x.LastUpdated)
            .HasDefaultValueSql("NOW()");

        builder.HasOne(x => x.Product)
            .WithMany(p => p.StockLevels)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Warehouse)
            .WithMany(w => w.StockLevels)
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BinLocation)
            .WithMany(b => b.StockLevels)
            .HasForeignKey(x => x.BinLocationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.ProductId, x.WarehouseId });
        builder.HasIndex(x => new { x.ProductId, x.WarehouseId, x.BinLocationId }).IsUnique();
    }
}

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.MovementType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ReferenceType)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne(x => x.Product)
            .WithMany(p => p.StockMovements)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Warehouse)
            .WithMany(w => w.StockMovements)
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BinLocation)
            .WithMany()
            .HasForeignKey(x => x.BinLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PerformedByUser)
            .WithMany()
            .HasForeignKey(x => x.PerformedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ProductId, x.CreatedAt });
        builder.HasIndex(x => new { x.WarehouseId, x.CreatedAt });
        builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
    }
}

public class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        builder.ToTable("stock_adjustments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.AdjustmentNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Reason)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .HasDefaultValue(AdjustmentStatus.Pending);

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.HasOne(x => x.Product)
            .WithMany(p => p.StockAdjustments)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BinLocation)
            .WithMany()
            .HasForeignKey(x => x.BinLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PerformedByUser)
            .WithMany()
            .HasForeignKey(x => x.PerformedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.AdjustmentNumber).IsUnique();
    }
}

public class AlertConfigurationConfiguration : IEntityTypeConfiguration<AlertConfiguration>
{
    public void Configure(EntityTypeBuilder<AlertConfiguration> builder)
    {
        builder.ToTable("alert_configurations");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.LowStockThreshold)
            .HasDefaultValue(5);

        builder.Property(x => x.SmsAlert)
            .HasDefaultValue(false);

        builder.Property(x => x.EmailAlert)
            .HasDefaultValue(true);

        builder.Property(x => x.InAppAlert)
            .HasDefaultValue(true);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.HasOne(x => x.Product)
            .WithMany(p => p.AlertConfigurations)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Warehouse)
            .WithMany(w => w.AlertConfigurations)
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ProductId, x.WarehouseId }).IsUnique();
    }
}
