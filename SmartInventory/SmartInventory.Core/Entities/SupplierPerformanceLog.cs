namespace SmartInventory.Core.Entities;

public class SupplierPerformanceLog : BaseEntity
{
    public int PromisedDays { get; set; }
    public int ActualDays { get; set; }
    public decimal FillRate { get; set; } // 0.0 - 1.0
    public string? Notes { get; set; }

    public Guid SupplierId { get; set; }
    public Guid PurchaseOrderId { get; set; }

    public Supplier Supplier { get; set; } = null!;
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
}
