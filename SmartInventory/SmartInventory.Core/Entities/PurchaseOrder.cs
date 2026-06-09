using SmartInventory.Core.Attributes;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;


public class PurchaseOrder : BaseEntity
{
    [Sortable]
    public string PoNumber { get; set; } = null!;
    [Sortable]
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    [Sortable]
    public decimal TotalAmount { get; set; }
    public DateTime? ExpectedDelivery { get; set; }
    public DateTime? ActualDelivery { get; set; }
    public string? Notes { get; set; }

    public string? SupplierNotes { get; set; }

    public string? TrackingNumber { get; set; }

    public DateTime? DispatchedAt { get; set; }

    public DateTime? SupplierCommittedDeliveryDate { get; set; }

    public bool? SupplierAccepted { get; set; }

    public Guid SupplierId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? ApprovedBy { get; set; }

    public Supplier Supplier { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public User? ApprovedByUser { get; set; }
    public ICollection<PurchaseOrderItem> Items { get; set; } = [];
    public ICollection<GoodsReceipt> GoodsReceipts { get; set; } = [];
    public ICollection<SupplierPerformanceLog> PerformanceLogs { get; set; } = [];
    public ICollection<SupplierInvoice> SupplierInvoices { get; set; } = [];
    public ICollection<PurchaseOrderShipment> Shipments { get; set; } = [];
}
