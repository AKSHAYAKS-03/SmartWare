using System;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.DTOs;

#region StockLevel DTOs
public class StockLevelResponseDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSKU { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid? BinLocationId { get; set; }
    public string? BinLocationCode { get; set; }
    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }
    public int QuantityOnOrder { get; set; }
    public int QuantityAvailable => QuantityOnHand - QuantityReserved;
    public DateTime LastUpdated { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StockLevelQueryParameters : QueryParameters
{
    public Guid? ProductId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? BinLocationId { get; set; }
}
#endregion

#region StockMovement DTOs
public class StockMovementCreateDto
{
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? BinLocationId { get; set; }
    public MovementType MovementType { get; set; }
    public int Quantity { get; set; }
    public ReferenceType ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public Guid PerformedBy { get; set; }
}

public class StockMovementResponseDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSKU { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid? BinLocationId { get; set; }
    public string? BinLocationCode { get; set; }
    public MovementType MovementType { get; set; }
    public string MovementTypeName => MovementType.ToString();
    public int Quantity { get; set; }
    public ReferenceType ReferenceType { get; set; }
    public string ReferenceTypeName => ReferenceType.ToString();
    public Guid? ReferenceId { get; set; }
    public Guid PerformedBy { get; set; }
    public string PerformedByUserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class StockMovementQueryParameters : QueryParameters
{
    public Guid? ProductId { get; set; }
    public Guid? WarehouseId { get; set; }
    public MovementType? MovementType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
#endregion

#region StockAdjustment DTOs
public class StockAdjustmentCreateDto
{
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? BinLocationId { get; set; }
    public AdjustmentReason Reason { get; set; }
    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }
    public string? Notes { get; set; }
    public Guid PerformedBy { get; set; }
}

public class StockAdjustmentApprovalDto
{
    public Guid ApprovedBy { get; set; }
    public bool Approve { get; set; }
}

public class StockAdjustmentResponseDto
{
    public Guid Id { get; set; }
    public string AdjustmentNumber { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSKU { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid? BinLocationId { get; set; }
    public string? BinLocationCode { get; set; }
    public AdjustmentReason Reason { get; set; }
    public string ReasonName => Reason.ToString();
    public AdjustmentStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }
    public int QuantityChange { get; set; }
    public string? Notes { get; set; }
    public Guid PerformedBy { get; set; }
    public string PerformedByUserName { get; set; } = string.Empty;
    public Guid? ApprovedBy { get; set; }
    public string? ApprovedByUserName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StockAdjustmentQueryParameters : QueryParameters
{
    public Guid? ProductId { get; set; }
    public Guid? WarehouseId { get; set; }
    public AdjustmentStatus? Status { get; set; }
}
#endregion
