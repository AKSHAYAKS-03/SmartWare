using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInventory.Core.Entities;

namespace SmartInventory.Repository.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.ContactPerson)
            .HasMaxLength(100);

        builder.Property(x => x.Email)
            .HasMaxLength(150);

        builder.Property(x => x.Phone)
            .HasMaxLength(20);

        builder.Property(x => x.Address)
            .HasMaxLength(250);

        builder.Property(x => x.PaymentTerms)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.CreditLimit)
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(x => x.Rating)
            .HasPrecision(3, 2)
            .HasDefaultValue(0);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public class SupplierProductConfiguration : IEntityTypeConfiguration<SupplierProduct>
{
    public void Configure(EntityTypeBuilder<SupplierProduct> builder)
    {
        builder.ToTable("supplier_products");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.UnitPrice)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.LeadTimeDays)
            .HasDefaultValue(0);

        builder.Property(x => x.MinOrderQuantity)
            .HasDefaultValue(1);

        builder.Property(x => x.IsPreferred)
            .HasDefaultValue(false);

        builder.HasOne(x => x.Supplier)
            .WithMany(s => s.SupplierProducts)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany(p => p.SupplierProducts)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.SupplierId, x.ProductId }).IsUnique();
    }
}

public class SupplierPerformanceLogConfiguration : IEntityTypeConfiguration<SupplierPerformanceLog>
{
    public void Configure(EntityTypeBuilder<SupplierPerformanceLog> builder)
    {
        builder.ToTable("supplier_performance_logs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.PromisedDays)
            .IsRequired();

        builder.Property(x => x.ActualDays)
            .IsRequired();

        builder.Property(x => x.FillRate)
            .HasPrecision(5, 4)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.HasOne(x => x.Supplier)
            .WithMany(s => s.PerformanceLogs)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.PurchaseOrder)
            .WithMany(p => p.PerformanceLogs)
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
