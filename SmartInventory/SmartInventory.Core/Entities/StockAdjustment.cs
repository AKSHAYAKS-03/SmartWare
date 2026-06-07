using SmartInventory.Core.Attributes;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Manual stock correction with approval workflow.
/// Extended with ShrinkageReason — used when the adjustment reason is shrinkage-related
/// (theft, damage, expiry, admin error). Enables the shrinkage tracking report.
/// </summary>
public class StockAdjustment : BaseEntity
{
    public string AdjustmentNumber { get; set; } = null!;
    public AdjustmentReason Reason { get; set; }
    [Sortable]
    public AdjustmentStatus Status { get; set; } = AdjustmentStatus.Pending;
    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }
    public int QuantityChange { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Specific shrinkage cause — only populated when Reason is Shrinkage.
    /// Drives the shrinkage analytics report and management alerts.
    /// </summary>
    public ShrinkageReason? ShrinkageReason { get; set; }

    /// <summary>Source document for system-generated adjustments (e.g. transfer variance).</summary>
    public ReferenceType? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }

    // ── Foreign Keys ─────────────────────────────────────────────────────────
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? BinLocationId { get; set; }
    public Guid PerformedBy { get; set; }
    public Guid? ApprovedBy { get; set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public Product Product { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public BinLocation? BinLocation { get; set; }
    public User PerformedByUser { get; set; } = null!;
    public User? ApprovedByUser { get; set; }
}
