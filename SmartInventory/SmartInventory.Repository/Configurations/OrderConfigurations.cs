using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInventory.Core.Entities;

namespace SmartInventory.Repository.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.PoNumber)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValueSql("CONCAT('PO-', TO_CHAR(CURRENT_DATE, 'YYYY'), '-', LPAD(nextval('seq_purchase_orders')::text, 6, '0'))")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.TotalAmount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.HasOne(x => x.Supplier)
            .WithMany(s => s.PurchaseOrders)
            .HasForeignKey(x => x.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PoNumber).IsUnique();
        builder.HasIndex(x => new { x.SupplierId, x.Status });
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
    }
}

public class PurchaseOrderItemConfiguration : IEntityTypeConfiguration<PurchaseOrderItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderItem> builder)
    {
        builder.ToTable("purchase_order_items");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.QuantityOrdered)
            .IsRequired();

        builder.Property(x => x.QuantityReceived)
            .HasDefaultValue(0);

        builder.Property(x => x.UnitPrice)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(x => x.TotalPrice)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.HasOne(x => x.PurchaseOrder)
            .WithMany(po => po.Items)
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany(p => p.PurchaseOrderItems)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class GoodsReceiptConfiguration : IEntityTypeConfiguration<GoodsReceipt>
{
    public void Configure(EntityTypeBuilder<GoodsReceipt> builder)
    {
        builder.ToTable("goods_receipts");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.GrnNumber)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValueSql("CONCAT('GRN-', TO_CHAR(CURRENT_DATE, 'YYYY'), '-', LPAD(nextval('seq_goods_receipts')::text, 6, '0'))")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.ReceivedDate)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.HasOne(x => x.PurchaseOrder)
            .WithMany(po => po.GoodsReceipts)
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReceivedByUser)
            .WithMany()
            .HasForeignKey(x => x.ReceivedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PurchaseOrderShipment)
            .WithMany(s => s.GoodsReceipts)
            .HasForeignKey(x => x.PurchaseOrderShipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.GrnNumber).IsUnique();
        builder.HasIndex(x => x.PurchaseOrderShipmentId);
    }
}

public class PurchaseOrderShipmentConfiguration : IEntityTypeConfiguration<PurchaseOrderShipment>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderShipment> builder)
    {
        builder.ToTable("PurchaseOrderShipments");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.ShipmentNumber)
            .IsRequired()
            .HasMaxLength(50);
        builder.Property(x => x.ShipmentNumber)
            .HasDefaultValueSql("CONCAT('SHP-', TO_CHAR(CURRENT_DATE, 'YYYY'), '-', LPAD(nextval('seq_shipments')::text, 6, '0'))")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.TrackingNumber).HasMaxLength(100);
        builder.Property(x => x.TrackingNumber)
            .HasDefaultValueSql("CONCAT('TRK-', TO_CHAR(CURRENT_DATE, 'YYYY'), '-', LPAD(nextval('seq_tracking_numbers')::text, 6, '0'))")
            .ValueGeneratedOnAdd();
        builder.Property(x => x.CarrierName).HasMaxLength(100);
        builder.Property(x => x.SupplierNotes).HasMaxLength(500);

        builder.HasOne(x => x.PurchaseOrder)
            .WithMany(po => po.Shipments)
            .HasForeignKey(x => x.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ShipmentNumber).IsUnique();
        builder.HasIndex(x => x.PurchaseOrderId);
    }
}

public class PurchaseOrderShipmentItemConfiguration : IEntityTypeConfiguration<PurchaseOrderShipmentItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderShipmentItem> builder)
    {
        builder.ToTable("PurchaseOrderShipmentItems");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.HasOne(x => x.PurchaseOrderShipment)
            .WithMany(s => s.Items)
            .HasForeignKey(x => x.PurchaseOrderShipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.PurchaseOrderItem)
            .WithMany()
            .HasForeignKey(x => x.PurchaseOrderItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PurchaseOrderShipmentId);
        builder.HasIndex(x => x.PurchaseOrderItemId);
    }
}

public class GoodsReceiptItemConfiguration : IEntityTypeConfiguration<GoodsReceiptItem>
{
    public void Configure(EntityTypeBuilder<GoodsReceiptItem> builder)
    {
        builder.ToTable("goods_receipt_items");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.QuantityReceived)
            .IsRequired();

        builder.Property(x => x.QuantityRejected)
            .HasDefaultValue(0);

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(250);

        builder.HasOne(x => x.GoodsReceipt)
            .WithMany(gr => gr.Items)
            .HasForeignKey(x => x.GoodsReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.PurchaseOrderItem)
            .WithMany(poi => poi.GoodsReceiptItems)
            .HasForeignKey(x => x.PurchaseOrderItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BinLocation)
            .WithMany()
            .HasForeignKey(x => x.BinLocationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
