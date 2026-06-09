using SmartInventory.Core.Enums;

namespace SmartInventory.Core.DTOs.SupplierPortal;

// REQUEST DTOs

public record SupplierUploadInvoiceRequest(
    Guid PurchaseOrderId,
    decimal Amount,
    string Currency,
    DateTime InvoiceDate,
    Stream FileStream,
    string FileName,
    string ContentType
);

// RESPONSE DTOs

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
