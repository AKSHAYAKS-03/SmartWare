using SmartInventory.Core.Interfaces;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Per-product, per-warehouse alert threshold configuration.
/// </summary>
public class AlertConfiguration : BaseEntity, ISoftDelete
{
    public int LowStockThreshold { get; set; }
    public bool SmsAlert { get; set; } = false;
    public bool EmailAlert { get; set; } = true;
    public bool InAppAlert { get; set; } = true;
    public bool IsActive { get; set; } = true;

    // Foreign Keys
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
}
