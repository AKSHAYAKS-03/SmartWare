namespace SmartInventory.Core.Entities;

public class PurchaseOrderShipmentItem : BaseEntity
{
    public Guid PurchaseOrderShipmentId { get; set; }
    public PurchaseOrderShipment PurchaseOrderShipment { get; set; } = null!;

    public Guid PurchaseOrderItemId { get; set; }
    public PurchaseOrderItem PurchaseOrderItem { get; set; } = null!;

    public int QuantityDispatched { get; set; }
}
