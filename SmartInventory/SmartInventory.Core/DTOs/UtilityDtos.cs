using System;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.DTOs;

#region Barcode DTOs
public class BarcodeResponseDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSKU { get; set; } = string.Empty;
    public string BarcodeValue { get; set; } = string.Empty;
    public BarcodeType BarcodeType { get; set; }
    public string BarcodeTypeName => BarcodeType.ToString();
    public bool IsPrimary { get; set; }
    public string? ImagePath { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BarcodeScanDto
{
    public string BarcodeValue { get; set; } = string.Empty;
    public Guid ScannedBy { get; set; }
    public Guid WarehouseId { get; set; }
    public ScanAction Action { get; set; }
}

public class BarcodeScanLogResponseDto
{
    public Guid Id { get; set; }
    public Guid BarcodeId { get; set; }
    public string BarcodeValue { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductSKU { get; set; } = string.Empty;
    public Guid ScannedBy { get; set; }
    public string ScannedByUserName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public ScanAction Action { get; set; }
    public string ActionName => Action.ToString();
    public DateTime ScannedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BarcodeGenerateRequestDto
{
    public Guid ProductId { get; set; }
    public BarcodeType Type { get; set; } = BarcodeType.Code128;
    public bool IsPrimary { get; set; } = false;
}

public class BarcodeUpdateDto
{
    public BarcodeType Type { get; set; } = BarcodeType.Code128;
}

public class StockLocationDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid ZoneId { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public Guid BinId { get; set; }
    public string BinCode { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
}

public class ScanResultDto
{
    public string Message { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSKU { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public string ABCClass { get; set; } = string.Empty;
    
    public int TotalOnHand { get; set; }
    public int TotalReserved { get; set; }
    public int TotalAvailable => TotalOnHand - TotalReserved;
    
    public System.Collections.Generic.List<StockLocationDto> Locations { get; set; } = new();
    
    public string ScanLogConfirmation { get; set; } = string.Empty;
}

public class BarcodeGoodsReceiptItemDto
{
    public string BarcodeValue { get; set; } = string.Empty;
    public string BinBarcode { get; set; } = string.Empty;
    public int QuantityReceived { get; set; }
    public int QuantityRejected { get; set; }
    public string? RejectionReason { get; set; }
    public string? OverrideReason { get; set; }
}

public class BarcodeGoodsReceiptCreateDto
{
    public Guid PurchaseOrderId { get; set; }
    public Guid? PurchaseOrderShipmentId { get; set; }
    public Guid ReceivedBy { get; set; }
    public Guid WarehouseId { get; set; }
    public string? Notes { get; set; }
    public string? IdempotencyKey { get; set; }
    public bool BypassWarnings { get; set; }
    public List<BarcodeGoodsReceiptItemDto> Items { get; set; } = [];
    public List<Guid> AttachmentIds { get; set; } = [];
}

public class BarcodeScanReceiptValidationDto
{
    public Guid? PurchaseOrderId { get; set; }
    public Guid? PurchaseOrderShipmentId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string BarcodeValue { get; set; } = string.Empty;
    public string? BinBarcode { get; set; }
}

public class BarcodeScanReceiptValidationResultDto
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSKU { get; set; } = string.Empty;
    public Guid? PurchaseOrderId { get; set; }
    public string? PurchaseOrderNumber { get; set; }
    public Guid? PurchaseOrderItemId { get; set; }
    public int QuantityOrdered { get; set; }
    public int QuantityReceived { get; set; }
    public int QuantityRemaining { get; set; }
    public Guid? BinLocationId { get; set; }
    public string? BinCode { get; set; }
    public string? WarehouseName { get; set; }
}

public class BarcodeScanTransferValidationDto
{
    public Guid TransferId { get; set; }
    public string BarcodeValue { get; set; } = string.Empty;
    public Guid? WarehouseId { get; set; }
    public string? BinBarcode { get; set; }
}

public class BarcodeScanTransferValidationResultDto
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSKU { get; set; } = string.Empty;
    public Guid TransferId { get; set; }
    public Guid? TransferItemId { get; set; }
    public int QuantityRequested { get; set; }
    public int QuantityDispatched { get; set; }
    public int QuantityRemaining { get; set; }
    public Guid? BinLocationId { get; set; }
    public string? BinCode { get; set; }
    public string? WarehouseName { get; set; }
}
#endregion

#region FileAttachment DTOs
public class FileAttachmentResponseDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DocumentCategory Category { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsVerified { get; set; }
    public Guid? VerifiedBy { get; set; }
    public Guid UploadedBy { get; set; }
    public string UploadedByUserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
#endregion

#region Report DTOs
public class InventoryValuationDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSKU { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int TotalStock { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalValue => TotalStock * UnitCost;
}

public class StockMovementTrendDto
{
    public DateTime Date { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public int TransactionCount { get; set; }
}

public class SupplierPerformanceDto
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string SupplierCode { get; set; } = string.Empty;
    public int TotalOrders { get; set; }
    public double AverageLeadTimeDays { get; set; }
    public decimal AverageFillRate { get; set; }
    public decimal PerformanceRating { get; set; }
}

public class DeadStockDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSKU { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public DateTime? LastMovementDate { get; set; }
    public int DaysSinceLastMovement { get; set; }
}

public class WarehouseUtilizationDto
{
    public Guid ZoneId { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public decimal TotalVolumeCapacity { get; set; }
    public decimal UtilizedVolume { get; set; }
    public decimal UtilizationPercentage => TotalVolumeCapacity > 0 ? (UtilizedVolume / TotalVolumeCapacity) * 100 : 0;
}

public class TransferVarianceReportDto
{
    public Guid TransferId { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public Guid TransferItemId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSKU { get; set; } = string.Empty;
    public string FromWarehouseName { get; set; } = string.Empty;
    public string ToWarehouseName { get; set; } = string.Empty;
    public int QuantityRequested { get; set; }
    public int QuantityDispatched { get; set; }
    public int QuantityReceived { get; set; }
    public int VarianceQuantity { get; set; }
    public TransferStatus TransferStatus { get; set; }
    public string TransferStatusName => TransferStatus.ToString();
    public Guid? AdjustmentId { get; set; }
    public AdjustmentStatus? AdjustmentStatus { get; set; }
    public string? AdjustmentStatusName => AdjustmentStatus?.ToString();
    public TransferVarianceResolutionStatus? VarianceResolutionStatus { get; set; }
    public string? VarianceResolutionStatusName => VarianceResolutionStatus?.ToString();
    public string? ApprovedByUserName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public decimal EstimatedLossValue { get; set; }
    public DateTime ReceivedDate { get; set; }
}

public class TransferVarianceSummaryDto
{
    public int TotalVariances { get; set; }
    public int PendingApproval { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public decimal TotalEstimatedLoss { get; set; }
    public decimal PendingLossValue { get; set; }
}

public class OverrideAuditReportDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string RuleBroken { get; set; } = string.Empty;
    public string OverrideReason { get; set; } = string.Empty;
    public Guid? TargetBinId { get; set; }
    public string? TargetBinCode { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
}
#endregion
