using System;
using System.Collections.Generic;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.DTOs;

#region PurchaseOrder DTOs
public class PurchaseOrderItemDto
{
    public Guid ProductId { get; set; }
    public int QuantityOrdered { get; set; }
    public decimal UnitPrice { get; set; }
}

public class PurchaseOrderCreateDto
{
    public Guid SupplierId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime? ExpectedDelivery { get; set; }
    public string? Notes { get; set; }
    public string? IdempotencyKey { get; set; }
    public List<PurchaseOrderItemDto> Items { get; set; } = [];
}

public class PurchaseOrderUpdateDto
{
    public Guid SupplierId { get; set; }
    public Guid WarehouseId { get; set; }
    public DateTime? ExpectedDelivery { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseOrderItemDto> Items { get; set; } = [];
}

public class PurchaseOrderItemResponseDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSKU { get; set; } = string.Empty;
    public int QuantityOrdered { get; set; }
    public int QuantityReceived { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public class PurchaseOrderResponseDto
{
    public Guid Id { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public PurchaseOrderStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public decimal TotalAmount { get; set; }
    public DateTime? ExpectedDelivery { get; set; }
    public DateTime? ActualDelivery { get; set; }
    public string? Notes { get; set; }

    // ── Supplier Portal Fields ─
    public string? SupplierNotes { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public bool? SupplierAccepted { get; set; }

    // The delivery date the supplier promised when accepting this PO.
   // Compare against ActualDelivery to measure on-time performance.
    public DateTime? SupplierCommittedDeliveryDate { get; set; }

    public Guid CreatedBy { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;
    public Guid? ApprovedBy { get; set; }
    public string? ApprovedByUserName { get; set; }
    public List<PurchaseOrderItemResponseDto> Items { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public class PurchaseOrderApprovalDto
{
    public bool Approve { get; set; }
    public string? RejectionReason { get; set; }
}

public class PurchaseOrderQueryParameters : QueryParameters
{
    public Guid? SupplierId { get; set; }
    public Guid? WarehouseId { get; set; }
    public PurchaseOrderStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
#endregion

#region GoodsReceipt DTOs
public class GoodsReceiptItemDto
{
    public Guid PurchaseOrderItemId { get; set; }
    //Required. Stock must always be placed in a specific bin. Create bins before receiving goods.
    public Guid BinLocationId { get; set; }
    public int QuantityReceived { get; set; }
    public int QuantityRejected { get; set; }
    public string? RejectionReason { get; set; }
    public string? OverrideReason { get; set; }
}

public class GoodsReceiptCreateDto
{
    public Guid PurchaseOrderId { get; set; }
    //Required when the PO has supplier shipments; optional for legacy PO-only receipts.
    public Guid? PurchaseOrderShipmentId { get; set; }
    public Guid ReceivedBy { get; set; }
    public Guid WarehouseId { get; set; }
    public string? Notes { get; set; }
    public string? IdempotencyKey { get; set; }
    public bool BypassWarnings { get; set; }
    public List<GoodsReceiptItemDto> Items { get; set; } = [];
    public List<Guid> AttachmentIds { get; set; } = [];
}

public class GoodsReceiptItemResponseDto
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderItemId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSKU { get; set; } = string.Empty;
    public Guid? BinLocationId { get; set; }
    public string? BinLocationCode { get; set; }
    public int QuantityReceived { get; set; }
    public int QuantityRejected { get; set; }
    public string? RejectionReason { get; set; }
}

public class GoodsReceiptResponseDto
{
    public Guid Id { get; set; }
    public string GrnNumber { get; set; } = string.Empty;
    public Guid PurchaseOrderId { get; set; }
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public Guid? PurchaseOrderShipmentId { get; set; }
    public string? ShipmentNumber { get; set; }
    public Guid ReceivedBy { get; set; }
    public string ReceivedByUserName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public GoodsReceiptStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? Notes { get; set; }
    public List<GoodsReceiptItemResponseDto> Items { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public class GoodsReceiptQueryParameters : QueryParameters
{
    public Guid? PurchaseOrderId { get; set; }
    public Guid? WarehouseId { get; set; }
    public GoodsReceiptStatus? Status { get; set; }
}
#endregion
