using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;

namespace SmartInventory.Repository.Configurations;

public class BarcodeConfiguration : IEntityTypeConfiguration<Barcode>
{
    public void Configure(EntityTypeBuilder<Barcode> builder)
    {
        builder.ToTable("barcodes");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.BarcodeValue)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.BarcodeType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.IsPrimary)
            .HasDefaultValue(true);

        builder.Property(x => x.ImagePath)
            .HasMaxLength(500);

        builder.HasOne(x => x.Product)
            .WithMany(p => p.Barcodes)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.BarcodeValue).IsUnique();
    }
}

public class BarcodeScanLogConfiguration : IEntityTypeConfiguration<BarcodeScanLog>
{
    public void Configure(EntityTypeBuilder<BarcodeScanLog> builder)
    {
        builder.ToTable("barcode_scan_logs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.Action)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ScannedAt)
            .HasDefaultValueSql("NOW()");

        builder.HasOne(x => x.Barcode)
            .WithMany(b => b.ScanLogs)
            .HasForeignKey(x => x.BarcodeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ScannedByUser)
            .WithMany()
            .HasForeignKey(x => x.ScannedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ScannedAt);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.Channel)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.Message)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.EntityType)
            .HasMaxLength(100);

        builder.Property(x => x.IsRead)
            .HasDefaultValue(false);

        builder.HasOne(x => x.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.UserId, x.IsRead });
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}

public class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("notification_logs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.Channel)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.EventType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Recipient)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(500);

        builder.Property(x => x.RetryCount)
            .HasDefaultValue(0);

        builder.HasOne(x => x.User)
            .WithMany(u => u.NotificationLogs)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class FileAttachmentConfiguration : IEntityTypeConfiguration<FileAttachment>
{
    public void Configure(EntityTypeBuilder<FileAttachment> builder)
    {
        builder.ToTable("file_attachments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.EntityType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(x => x.FilePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.MimeType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.FileSizeBytes)
            .IsRequired();

        builder.HasOne(x => x.UploadedByUser)
            .WithMany()
            .HasForeignKey(x => x.UploadedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}

public class SequenceCounterConfiguration : IEntityTypeConfiguration<SequenceCounter>
{
    public void Configure(EntityTypeBuilder<SequenceCounter> builder)
    {
        builder.ToTable("sequence_counters");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.EntityName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Prefix)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.CurrentValue)
            .HasDefaultValue(0);

        builder.HasIndex(x => x.EntityName).IsUnique();
    }
}

public class SupplierInvoiceConfiguration : IEntityTypeConfiguration<SupplierInvoice>
{
    public void Configure(EntityTypeBuilder<SupplierInvoice> builder)
    {
        builder.ToTable("SupplierInvoices");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValueSql("CONCAT('INV-', TO_CHAR(CURRENT_DATE, 'YYYY'), '-', LPAD(nextval('seq_invoices')::text, 6, '0'))")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Amount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasMaxLength(3)
            .HasDefaultValue("INR");

        builder.Property(x => x.FilePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.OriginalFileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired()
            .HasDefaultValue(SupplierInvoiceStatus.Pending);

        builder.Property(x => x.InternalNotes)
            .HasMaxLength(1000);

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(500);

        builder.Property(x => x.PaymentReference)
            .HasMaxLength(100);

        builder.HasOne(x => x.Supplier)
            .WithMany(s => s.Invoices)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PurchaseOrder)
            .WithMany(po => po.SupplierInvoices)
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UploadedByContact)
            .WithMany()
            .HasForeignKey(x => x.UploadedByContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.InvoiceNumber).IsUnique();
        builder.HasIndex(x => new { x.SupplierId, x.PurchaseOrderId });
    }
}

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.EventType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Payload)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("Pending");

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(1000);

        builder.Property(x => x.RetryCount)
            .HasDefaultValue(0);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAt);
    }
}
