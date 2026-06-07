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
            .HasMaxLength(20)
            .HasDefaultValueSql("CONCAT('SUP-', TO_CHAR(CURRENT_DATE, 'YYYY'), '-', LPAD(nextval('seq_suppliers')::text, 5, '0'))")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.GSTIN)
            .HasMaxLength(15);

        builder.Property(x => x.PAN)
            .HasMaxLength(10);

        builder.Property(x => x.ContactPerson)
            .HasMaxLength(100);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.Phone)
            .IsRequired()
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

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.RegistrationSource)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.InviteToken)
            .HasMaxLength(100);

        builder.Property(x => x.AgreementSignedIp)
            .HasMaxLength(50);

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(500);

        builder.Property(x => x.SuspensionReason)
            .HasMaxLength(500);

        builder.Property(x => x.InfoRequestedMessage)
            .HasMaxLength(500);

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

public class SupplierContactConfiguration : IEntityTypeConfiguration<SupplierContact>
{
    public void Configure(EntityTypeBuilder<SupplierContact> builder)
    {
        builder.ToTable("SupplierContacts");

        builder.Property(x => x.FullName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.PasswordHash)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(x => x.Phone)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.JobTitle)
            .HasMaxLength(100);

        builder.Property(x => x.EmailVerifyToken)
            .HasMaxLength(100);

        builder.Property(x => x.EmailVerified)
            .HasDefaultValue(false);
    }
}
