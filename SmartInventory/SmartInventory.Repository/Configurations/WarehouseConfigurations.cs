using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Enums;


namespace SmartInventory.Repository.Configurations;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("warehouses");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValueSql("CONCAT('WH-', TO_CHAR(CURRENT_DATE, 'YYYY'), '-', LPAD(nextval('seq_warehouses')::text, 6, '0'))")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Address)
            .HasMaxLength(250);

        builder.Property(x => x.City)
            .HasMaxLength(100);

        builder.Property(x => x.Country)
            .HasMaxLength(100);

        builder.Property(x => x.State)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.PostalCode)
            .HasMaxLength(20);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.Property(x => x.ContactPerson)
            .HasMaxLength(100);

        builder.Property(x => x.ContactNumber)
            .HasMaxLength(50);

        builder.Property(x => x.Email)
            .HasMaxLength(100);

        builder.Property(x => x.GSTIN)
            .HasMaxLength(15);

        builder.Property(x => x.RegistrationNumber)
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired()
            .HasDefaultValue(WarehouseStatus.PendingVerification);

        builder.HasOne(x => x.Manager)
            .WithMany()
            .HasForeignKey(x => x.ManagerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.GSTIN);
        builder.HasIndex(x => x.RegistrationNumber).IsUnique().HasFilter("\"RegistrationNumber\" IS NOT NULL");
        builder.HasIndex(x => x.Status);
    }
}

public class WarehouseZoneConfiguration : IEntityTypeConfiguration<WarehouseZone>
{
    public void Configure(EntityTypeBuilder<WarehouseZone> builder)
    {
        builder.ToTable("warehouse_zones");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValueSql("CONCAT('ZN-', TO_CHAR(CURRENT_DATE, 'YYYY'), '-', LPAD(nextval('seq_zones')::text, 6, '0'))")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.ZoneType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.HasOne(x => x.Warehouse)
            .WithMany(w => w.Zones)
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.WarehouseId, x.Code }).IsUnique();
    }
}

public class BinLocationConfiguration : IEntityTypeConfiguration<BinLocation>
{
    public void Configure(EntityTypeBuilder<BinLocation> builder)
    {
        builder.ToTable("bin_locations");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.BinCode)
            .IsRequired()
            .HasMaxLength(30)
            .HasDefaultValueSql("CONCAT('BIN-', TO_CHAR(CURRENT_DATE, 'YYYY'), '-', LPAD(nextval('seq_bins')::text, 6, '0'))")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Barcode)
            .HasMaxLength(50);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true);

        builder.HasOne(x => x.Zone)
            .WithMany(z => z.BinLocations)
            .HasForeignKey(x => x.ZoneId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Barcode).IsUnique().HasFilter("\"Barcode\" IS NOT NULL");
        builder.HasIndex(x => new { x.ZoneId, x.BinCode }).IsUnique();
    }
}

public class UserWarehouseAccessConfiguration : IEntityTypeConfiguration<UserWarehouseAccess>
{
    public void Configure(EntityTypeBuilder<UserWarehouseAccess> builder)
    {
        builder.ToTable("user_warehouse_access");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(x => x.AccessLevel)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.GrantedAt)
            .HasDefaultValueSql("NOW()");

        builder.HasOne(x => x.User)
            .WithMany(u => u.WarehouseAccess)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Warehouse)
            .WithMany(w => w.UserAccess)
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.UserId, x.WarehouseId }).IsUnique();
    }
}
