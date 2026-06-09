using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;

namespace SmartInventory.Repository.Configurations;

public class WarehouseTransferConfiguration : IEntityTypeConfiguration<WarehouseTransfer>
{
    public void Configure(EntityTypeBuilder<WarehouseTransfer> builder)
    {
        builder.ToTable("warehouse_transfers");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.TransferNumber)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValueSql("CONCAT('TRF-', TO_CHAR(CURRENT_DATE, 'YYYY'), '-', LPAD(nextval('seq_transfers')::text, 6, '0'))")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(500);

        builder.Property(x => x.VarianceResolutionStatus)
            .HasConversion<int>();

        builder.HasOne(x => x.FromWarehouse)
            .WithMany()
            .HasForeignKey(x => x.FromWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ToWarehouse)
            .WithMany()
            .HasForeignKey(x => x.ToWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RequestedByUser)
            .WithMany()
            .HasForeignKey(x => x.RequestedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TransferNumber).IsUnique();
        builder.HasIndex(x => new { x.FromWarehouseId, x.ToWarehouseId, x.Status });
    }
}

public class TransferItemConfiguration : IEntityTypeConfiguration<TransferItem>
{
    public void Configure(EntityTypeBuilder<TransferItem> builder)
    {
        builder.ToTable("transfer_items");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.QuantityRequested)
            .IsRequired();

        builder.Property(x => x.QuantityDispatched)
            .HasDefaultValue(0);

        builder.Property(x => x.QuantityReceived)
            .HasDefaultValue(0);

        builder.HasOne(x => x.Transfer)
            .WithMany(t => t.Items)
            .HasForeignKey(x => x.TransferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany(p => p.TransferItems)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FromBin)
            .WithMany()
            .HasForeignKey(x => x.FromBinId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ToBin)
            .WithMany()
            .HasForeignKey(x => x.ToBinId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
