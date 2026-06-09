using SmartInventory.Core.Attributes;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;


public class GoodsReceipt : BaseEntity
{
    public string GrnNumber { get; set; } = null!;
    public DateTime ReceivedDate { get; set; }
    [Sortable]
    public GoodsReceiptStatus Status { get; set; } = GoodsReceiptStatus.Pending;
    public string? Notes { get; set; }

    public Guid PurchaseOrderId { get; set; }
    public Guid ReceivedBy { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? PurchaseOrderShipmentId { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public PurchaseOrderShipment? PurchaseOrderShipment { get; set; }
    public User ReceivedByUser { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public ICollection<GoodsReceiptItem> Items { get; set; } = [];
}
