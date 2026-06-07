using SmartInventory.Core.Attributes;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Supplier-uploaded invoice document linked to a Purchase Order.
/// Suppliers upload PDFs; finance team reviews and processes payment.
/// </summary>
public class SupplierInvoice : BaseEntity
{
    /// <summary>Supplier-side invoice reference number (e.g., "INV-2026-001").</summary>
    [Sortable]
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>Total amount declared on the invoice.</summary>
    [Sortable]
    public decimal Amount { get; set; }

    /// <summary>Currency code, default to base currency (e.g., "INR").</summary>
    public string Currency { get; set; } = "INR";

    /// <summary>Date printed on the invoice document.</summary>
    public DateTime InvoiceDate { get; set; }

    /// <summary>Storage path or URL of the uploaded PDF file.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Original file name of the uploaded PDF.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Current review/payment status.</summary>
    [Sortable]
    public SupplierInvoiceStatus Status { get; set; } = SupplierInvoiceStatus.Pending;

    /// <summary>Finance team's internal notes (not visible to supplier).</summary>
    public string? InternalNotes { get; set; }

    /// <summary>Rejection reason communicated back to the supplier (if rejected).</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Amount approved for payment (may be less than total amount due to partial receipt).</summary>
    public decimal? ApprovedAmount { get; set; }

    /// <summary>Amount actually paid to the supplier.</summary>
    public decimal? PaidAmount { get; set; }

    /// <summary>Bank transfer ID, cheque number, or ERP journal ID.</summary>
    public string? PaymentReference { get; set; }

    /// <summary>UTC timestamp when payment was processed.</summary>
    public DateTime? PaidAt { get; set; }

    // Foreign Keys
    public Guid SupplierId { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public Guid UploadedByContactId { get; set; }

    // Navigation
    public Supplier Supplier { get; set; } = null!;
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public SupplierContact UploadedByContact { get; set; } = null!;
}
