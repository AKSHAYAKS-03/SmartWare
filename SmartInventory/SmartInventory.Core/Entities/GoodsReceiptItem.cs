using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;


public class GoodsReceiptItem : BaseEntity
{
    public int QuantityReceived { get; set; }
    public int QuantityRejected { get; set; }
    public string? RejectionReason { get; set; }

    public QualityCheckStatus QualityCheckStatus { get; set; } = QualityCheckStatus.Pending;


    public string? QualityCheckNotes { get; set; }

    public Guid GoodsReceiptId { get; set; }
    public Guid PurchaseOrderItemId { get; set; }
    public Guid? BinLocationId { get; set; }

    public GoodsReceipt GoodsReceipt { get; set; } = null!;
    public PurchaseOrderItem PurchaseOrderItem { get; set; } = null!;
    public BinLocation? BinLocation { get; set; }
}
