using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.Entities;
using SmartInventory.Repository;

namespace SmartInventory.Tests;

/// <summary>
/// Test DbContext that assigns document numbers in-memory (PostgreSQL defaults are not applied).
/// </summary>
public class TestAppDbContext : AppDbContext
{
    public TestAppDbContext(DbContextOptions<TestAppDbContext> options) : base(options) { }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AssignDocumentNumbers();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void AssignDocumentNumbers()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        foreach (var entry in ChangeTracker.Entries<StockAdjustment>()
            .Where(e => e.State == EntityState.Added))
        {
            if (string.IsNullOrWhiteSpace(entry.Entity.AdjustmentNumber) || entry.Entity.AdjustmentNumber == "TEMP")
                entry.Entity.AdjustmentNumber = $"ADJ-TEST-{suffix}";
        }

        foreach (var entry in ChangeTracker.Entries<PurchaseOrder>()
            .Where(e => e.State == EntityState.Added))
        {
            if (string.IsNullOrWhiteSpace(entry.Entity.PoNumber))
                entry.Entity.PoNumber = $"PO-TEST-{suffix}";
        }

        foreach (var entry in ChangeTracker.Entries<GoodsReceipt>()
            .Where(e => e.State == EntityState.Added))
        {
            if (string.IsNullOrWhiteSpace(entry.Entity.GrnNumber))
                entry.Entity.GrnNumber = $"GRN-TEST-{suffix}";
        }

        foreach (var entry in ChangeTracker.Entries<WarehouseTransfer>()
            .Where(e => e.State == EntityState.Added))
        {
            if (string.IsNullOrWhiteSpace(entry.Entity.TransferNumber))
                entry.Entity.TransferNumber = $"TRF-TEST-{suffix}";
        }

        foreach (var entry in ChangeTracker.Entries<Supplier>()
            .Where(e => e.State == EntityState.Added))
        {
            if (string.IsNullOrWhiteSpace(entry.Entity.Code))
                entry.Entity.Code = $"SUP-TEST-{suffix}";
        }

        foreach (var entry in ChangeTracker.Entries<PurchaseOrderShipment>()
            .Where(e => e.State == EntityState.Added))
        {
            if (string.IsNullOrWhiteSpace(entry.Entity.ShipmentNumber) || entry.Entity.ShipmentNumber == "TEMP")
                entry.Entity.ShipmentNumber = $"SHP-TEST-{suffix}";
        }
    }
}
