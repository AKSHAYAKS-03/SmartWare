using SmartInventory.Core.Enums;

namespace SmartInventory.Core.DTOs.SupplierPortal;

// REQUEST DTOs

public record SupplierRespondToPORequest(
    bool Accept,
    string? DeclineReason,
    DateTime? CommittedDeliveryDate
);

public record SupplierUpdateDeliveryDateRequest(
    DateTime ExpectedDelivery,
    string? SupplierNotes
);

public record SupplierMarkDispatchedRequest(
    string? SupplierNotes
);

public record SupplierCreateShipmentRequest(
    string? CarrierName,
    DateTime? ExpectedDelivery,
    string? SupplierNotes,
  
    //When null, ships all remaining quantities for each line
    List<SupplierShipmentLineRequest>? Lines
);

public record SupplierShipmentLineRequest(
    Guid PurchaseOrderItemId,
    int QuantityDispatched
);

// RESPONSE DTOs

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

        /// SUM((GrnItem.QuantityReceived - GrnItem.QuantityRejected) × POItem.UnitPrice)
    /// for all Accepted/PartiallyAccepted GRNs. Dynamically calculated.
    decimal AggregateAcceptedGrnValue,

        /// SUM(invoice.ApprovedAmount) WHERE Status IN (Matched, Paid). Dynamically calculated.
    decimal AggregateMatchedInvoiceValue,

        /// AggregateAcceptedGrnValue - AggregateMatchedInvoiceValue.
    /// How much the supplier can still legitimately invoice.
    decimal RemainingInvoiceableAmount,

    /// PO.TotalAmount - SUM(invoice.Amount) WHERE Status IN (Pending, UnderReview, Matched, Paid).
    /// How much of the PO is not yet covered by any invoice, even those still Pending.
    decimal RemainingUnbilledAmount
);


public record SupplierPOLineItemDto(
    Guid Id,
    string ProductName,
    string ProductSku,
    string UnitOfMeasure,
    int QuantityOrdered,
    int QuantityReceived,
    decimal UnitPrice,

    //QuantityReceived - QuantityRejected across all GRNs for this PO line item.
    int AcceptedQuantity,

    int RejectedQuantity,

    string? RejectionReason,

    //AcceptedQuantity × UnitPrice. 
    decimal InvoiceableAmount
);

public record SupplierShipmentLineResponseDto(
    Guid PurchaseOrderItemId,
    string ProductName,
    string ProductSku,
    int QuantityDispatched,
    decimal UnitPrice,
    decimal LineAmount
);

public record SupplierShipmentResponseDto(
    Guid Id,
    string ShipmentNumber,
    string? TrackingNumber,
    string? CarrierName,
    DateTime DispatchedAt,
    DateTime? ExpectedDelivery,
    string? SupplierNotes,
    decimal TotalAmount,
    List<SupplierShipmentLineResponseDto> Items
);
