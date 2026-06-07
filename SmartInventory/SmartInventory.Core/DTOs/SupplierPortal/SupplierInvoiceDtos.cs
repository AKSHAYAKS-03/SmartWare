using SmartInventory.Core.Enums;

namespace SmartInventory.Core.DTOs.SupplierPortal;

// ──────────────────────────────────────────────────────────────────────────────
// REQUEST DTOs
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Supplier uploads a new invoice PDF against a PO.</summary>
public record SupplierUploadInvoiceRequest(
    Guid PurchaseOrderId,
    string InvoiceNumber,
    decimal Amount,
    string Currency,
    DateTime InvoiceDate,
    Stream FileStream,
    string FileName,
    string ContentType
);

// ──────────────────────────────────────────────────────────────────────────────
// RESPONSE DTOs
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Summary of an invoice shown in the supplier's invoice history list.</summary>
public record SupplierInvoiceListItemDto(
    Guid Id,
    string InvoiceNumber,
    string PoNumber,
    decimal Amount,
    string Currency,
    DateTime InvoiceDate,
    SupplierInvoiceStatus Status,
    DateTime CreatedAt
);

/// <summary>Full invoice detail shown to the supplier (no internal finance notes).</summary>
public record SupplierInvoiceDetailDto(
    Guid Id,
    string InvoiceNumber,
    string PoNumber,
    decimal Amount,
    string Currency,
    DateTime InvoiceDate,
    SupplierInvoiceStatus Status,
    string OriginalFileName,
    string? RejectionReason,
    DateTime? PaidAt,
    DateTime CreatedAt
);
