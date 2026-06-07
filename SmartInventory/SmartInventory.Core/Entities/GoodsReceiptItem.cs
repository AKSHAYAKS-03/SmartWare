using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Line item within a goods receipt, linked to a PO item.
/// Extended with QualityCheckStatus and QualityCheckNotes for the
/// Final_Plan GRN Quality Check feature — prevents damaged or non-conforming
/// stock from entering the physical inventory.
/// </summary>
public class GoodsReceiptItem : BaseEntity
{
    public int QuantityReceived { get; set; }
    public int QuantityRejected { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>
    /// Result of the quality inspection performed on this line during GRN.
    /// Passed = added to inventory; Failed = rejected; PartiallyAccepted = mixed result.
    /// </summary>
    public QualityCheckStatus QualityCheckStatus { get; set; } = QualityCheckStatus.Pending;

    /// <summary>
    /// Free-text notes from the quality inspection — defect descriptions, batch numbers, etc.
    /// </summary>
    public string? QualityCheckNotes { get; set; }

    // ── Foreign Keys ─────────────────────────────────────────────────────────
    public Guid GoodsReceiptId { get; set; }
    public Guid PurchaseOrderItemId { get; set; }
    public Guid? BinLocationId { get; set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public GoodsReceipt GoodsReceipt { get; set; } = null!;
    public PurchaseOrderItem PurchaseOrderItem { get; set; } = null!;
    public BinLocation? BinLocation { get; set; }
}
