using SmartInventory.Core.Attributes;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;


public class SupplierInvoice : BaseEntity
{
    [Sortable]
    public string InvoiceNumber { get; set; } = null!;

    [Sortable]
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "INR";

    public DateTime InvoiceDate { get; set; }

    public string FilePath { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    [Sortable]
    public SupplierInvoiceStatus Status { get; set; } = SupplierInvoiceStatus.Pending;

    public string? InternalNotes { get; set; }

    public string? RejectionReason { get; set; }

    public decimal? ApprovedAmount { get; set; }

    public decimal? PaidAmount { get; set; }

    public string? PaymentReference { get; set; }

    public DateTime? PaidAt { get; set; }

    
    public Guid SupplierId { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public Guid UploadedByContactId { get; set; }

    public Supplier Supplier { get; set; } = null!;
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public SupplierContact UploadedByContact { get; set; } = null!;
}
