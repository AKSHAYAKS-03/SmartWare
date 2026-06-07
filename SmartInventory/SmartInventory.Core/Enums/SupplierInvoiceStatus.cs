using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Enums;

/// <summary>
/// Lifecycle status of a supplier-uploaded invoice.
/// </summary>
public enum SupplierInvoiceStatus
{
    /// <summary>Invoice has been uploaded but not yet reviewed by finance.</summary>
    Pending = 0,

    /// <summary>Invoice has been matched to the PO and is under review.</summary>
    UnderReview = 1,

    /// <summary>Invoice matches PO — approved for payment processing.</summary>
    Matched = 2,

    /// <summary>Invoice has discrepancies and was rejected.</summary>
    Rejected = 3,

    /// <summary>Payment has been completed.</summary>
    Paid = 4,

    /// <summary>Invoice was voided (e.g., duplicate or cancelled PO).</summary>
    Voided = 5
}
