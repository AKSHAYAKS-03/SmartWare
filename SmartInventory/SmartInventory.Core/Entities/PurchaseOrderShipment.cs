using SmartInventory.Core.Attributes;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;

public class PurchaseOrderShipment : BaseEntity
{
    [Sortable]
    public string ShipmentNumber { get; set; } = null!;
    public string? TrackingNumber { get; set; }
    public string? CarrierName { get; set; }
    public DateTime DispatchedAt { get; set; }
    public DateTime? ExpectedDelivery { get; set; }
    public string? SupplierNotes { get; set; }

    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public ICollection<PurchaseOrderShipmentItem> Items { get; set; } = [];
    public ICollection<GoodsReceipt> GoodsReceipts { get; set; } = [];
}
