using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Manual stock correction with approval workflow.
/// </summary>
public class StockAdjustment : BaseEntity
{
    public string AdjustmentNumber { get; set; } = string.Empty;
    public AdjustmentReason Reason { get; set; }
    public AdjustmentStatus Status { get; set; } = AdjustmentStatus.Pending;
    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }
    public int QuantityChange { get; set; }
    public string? Notes { get; set; }

    // Foreign Keys
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? BinLocationId { get; set; }
    public Guid PerformedBy { get; set; }
    public Guid? ApprovedBy { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public BinLocation? BinLocation { get; set; }
    public User PerformedByUser { get; set; } = null!;
    public User? ApprovedByUser { get; set; }
}
