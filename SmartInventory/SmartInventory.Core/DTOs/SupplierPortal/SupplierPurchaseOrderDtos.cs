using SmartInventory.Core.Enums;

namespace SmartInventory.Core.DTOs.SupplierPortal;

// ──────────────────────────────────────────────────────────────────────────────
// REQUEST DTOs
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Supplier accepts or declines a PO. When accepting, they must provide their committed delivery date.</summary>
public record SupplierRespondToPORequest(
    bool Accept,
    string? DeclineReason,
    /// <summary>The supplier's promised delivery date. Required when Accept = true.</summary>
    DateTime? CommittedDeliveryDate
);

/// <summary>Supplier updates the expected delivery date on a PO.</summary>
public record SupplierUpdateDeliveryDateRequest(
    DateTime ExpectedDelivery,
    string? SupplierNotes
);

/// <summary>Supplier marks an order as dispatched and optionally adds tracking.</summary>
public record SupplierMarkDispatchedRequest(
    string? TrackingNumber,
    string? SupplierNotes
);

/// <summary>Supplier creates an ASN/shipment with optional partial line quantities.</summary>
public record SupplierCreateShipmentRequest(
    string? TrackingNumber,
    string? CarrierName,
    DateTime? ExpectedDelivery,
    string? SupplierNotes,
    /// <summary>When null, ships all remaining quantities for each line.</summary>
    List<SupplierShipmentLineRequest>? Lines
);

public record SupplierShipmentLineRequest(
    Guid PurchaseOrderItemId,
    int QuantityDispatched
);

// ──────────────────────────────────────────────────────────────────────────────
// RESPONSE DTOs
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Purchase order as presented to the supplier — no internal cost notes or other supplier POs.</summary>
public record SupplierPOListItemDto(
    Guid Id,
    string PoNumber,
    PurchaseOrderStatus Status,
    bool? SupplierAccepted,
    decimal TotalAmount,
    DateTime? ExpectedDelivery,
    DateTime? SupplierCommittedDeliveryDate,
    DateTime? DispatchedAt,
    DateTime CreatedAt,
    string WarehouseName
);

/// <summary>
/// Full PO detail visible to the supplier including line items and invoice financial summary.
/// Financial summary fields are computed dynamically from GRN and Invoice data — not stored in DB.
/// </summary>
public record SupplierPODetailDto(
    Guid Id,
    string PoNumber,
    PurchaseOrderStatus Status,
    bool? SupplierAccepted,
    decimal TotalAmount,
    DateTime? ExpectedDelivery,
    DateTime? SupplierCommittedDeliveryDate,
    DateTime? DispatchedAt,
    string? TrackingNumber,
    string? Notes,
    string? SupplierNotes,
    DateTime CreatedAt,
    string WarehouseName,
    List<SupplierPOLineItemDto> Items,

    /// <summary>
    /// SUM((GrnItem.QuantityReceived - GrnItem.QuantityRejected) × POItem.UnitPrice)
    /// for all Accepted/PartiallyAccepted GRNs. Dynamically calculated.
    /// </summary>
    decimal AggregateAcceptedGrnValue,

    /// <summary>
    /// SUM(invoice.ApprovedAmount) WHERE Status IN (Matched, Paid). Dynamically calculated.
    /// </summary>
    decimal AggregateMatchedInvoiceValue,

    /// <summary>
    /// AggregateAcceptedGrnValue - AggregateMatchedInvoiceValue.
    /// How much the supplier can still legitimately invoice.
    /// </summary>
    decimal RemainingInvoiceableAmount,

    /// <summary>
    /// PO.TotalAmount - SUM(invoice.Amount) WHERE Status IN (Pending, UnderReview, Matched, Paid).
    /// How much of the PO is not yet covered by any invoice, even those still Pending.
    /// </summary>
    decimal RemainingUnbilledAmount
);

/// <summary>
/// A single line item in the PO as shown to the supplier.
/// Includes GRN acceptance/rejection breakdown computed from all GRNs for this PO item.
/// These fields are dynamically calculated — not stored in DB.
/// </summary>
public record SupplierPOLineItemDto(
    Guid Id,
    string ProductName,
    string ProductSku,
    string UnitOfMeasure,
    int QuantityOrdered,
    int QuantityReceived,
    decimal UnitPrice,

    /// <summary>QuantityReceived - QuantityRejected across all GRNs for this PO line item.</summary>
    int AcceptedQuantity,

    /// <summary>Total rejected quantity across all GRNs for this PO line item.</summary>
    int RejectedQuantity,

    /// <summary>Comma-separated rejection reasons from all GRN items for this PO line. Null if no rejections.</summary>
    string? RejectionReason,

    /// <summary>AcceptedQuantity × UnitPrice. The correct amount the supplier should invoice for this line.</summary>
    decimal InvoiceableAmount
);

public record SupplierShipmentLineResponseDto(
    Guid PurchaseOrderItemId,
    string ProductName,
    string ProductSku,
    int QuantityDispatched
);

public record SupplierShipmentResponseDto(
    Guid Id,
    string ShipmentNumber,
    string? TrackingNumber,
    string? CarrierName,
    DateTime DispatchedAt,
    DateTime? ExpectedDelivery,
    string? SupplierNotes,
    List<SupplierShipmentLineResponseDto> Items
);
