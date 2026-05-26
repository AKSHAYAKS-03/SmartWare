namespace SmartInventory.Core.Entities;

/// <summary>
/// Line item within a goods receipt, linked to a PO item.
/// </summary>
public class GoodsReceiptItem : BaseEntity
{
    public int QuantityReceived { get; set; }
    public int QuantityRejected { get; set; }
    public string? RejectionReason { get; set; }

    // Foreign Keys
    public Guid GoodsReceiptId { get; set; }
    public Guid PurchaseOrderItemId { get; set; }
    public Guid? BinLocationId { get; set; }

    // Navigation
    public GoodsReceipt GoodsReceipt { get; set; } = null!;
    public PurchaseOrderItem PurchaseOrderItem { get; set; } = null!;
    public BinLocation? BinLocation { get; set; }
}
