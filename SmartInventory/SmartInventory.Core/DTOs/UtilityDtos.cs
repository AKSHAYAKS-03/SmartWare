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
#endregion
