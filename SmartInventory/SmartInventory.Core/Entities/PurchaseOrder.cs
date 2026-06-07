using SmartInventory.Core.Attributes;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Purchase order with multi-level approval workflow.
/// </summary>
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

    // ── Supplier Portal Fields ────────────────────────────────────────────────
    /// <summary>Notes/comments added by the supplier (visible to internal team).</summary>
    public string? SupplierNotes { get; set; }

    /// <summary>Shipment tracking number added by the supplier upon dispatch.</summary>
    public string? TrackingNumber { get; set; }

    /// <summary>UTC timestamp when the supplier marked the order as dispatched.</summary>
    public DateTime? DispatchedAt { get; set; }

    /// <summary>
    /// The specific delivery date the supplier committed to upon accepting the PO.
    /// Used to calculate on-time delivery performance (compare against ActualDelivery).
    /// Null until the supplier responds to the PO.
    /// </summary>
    public DateTime? SupplierCommittedDeliveryDate { get; set; }

    /// <summary>Supplier-side acceptance status: null=pending, true=accepted, false=declined.</summary>
    public bool? SupplierAccepted { get; set; }

    // Foreign Keys
    public Guid SupplierId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? ApprovedBy { get; set; }

    // Navigation
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
