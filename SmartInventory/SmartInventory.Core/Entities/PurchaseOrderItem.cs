namespace SmartInventory.Core.Entities;

/// <summary>
/// Line item within a purchase order.
/// </summary>
public class PurchaseOrderItem : BaseEntity
{
    public int QuantityOrdered { get; set; }
    public int QuantityReceived { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }

    // Foreign Keys
    public Guid PurchaseOrderId { get; set; }
    public Guid ProductId { get; set; }

    // Navigation
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ICollection<GoodsReceiptItem> GoodsReceiptItems { get; set; } = [];
}
