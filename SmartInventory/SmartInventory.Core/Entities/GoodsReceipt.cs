using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Goods Receipt Note (GRN) created when a PO delivery arrives.
/// </summary>
public class GoodsReceipt : BaseEntity
{
    public string GrnNumber { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public GoodsReceiptStatus Status { get; set; } = GoodsReceiptStatus.Pending;
    public string? Notes { get; set; }

    // Foreign Keys
    public Guid PurchaseOrderId { get; set; }
    public Guid ReceivedBy { get; set; }
    public Guid WarehouseId { get; set; }

    // Navigation
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public User ReceivedByUser { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public ICollection<GoodsReceiptItem> Items { get; set; } = [];
}
