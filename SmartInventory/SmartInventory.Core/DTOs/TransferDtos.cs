using System;
using System.Collections.Generic;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.DTOs;

public class TransferItemDto
{
    public Guid ProductId { get; set; }
    public Guid? FromBinId { get; set; }
    public Guid? ToBinId { get; set; }
    public int QuantityRequested { get; set; }
}

public class TransferCreateDto
{
    public Guid FromWarehouseId { get; set; }
    public Guid ToWarehouseId { get; set; }
    public Guid RequestedBy { get; set; }
    public string? Notes { get; set; }
    public List<TransferItemDto> Items { get; set; } = [];
    public string? IdempotencyKey { get; set; }
}

public class TransferReceiveItemDto
{
    public Guid TransferItemId { get; set; }
    public int QuantityReceived { get; set; }
    public string? OverrideReason { get; set; }
}

public class TransferReceiveDto
{
    public bool BypassWarnings { get; set; }
    public List<TransferReceiveItemDto> Items { get; set; } = [];
}

public class TransferApprovalDto
{
    public Guid ApprovedBy { get; set; }
    public bool Approve { get; set; }
}

public class TransferItemResponseDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSKU { get; set; } = string.Empty;
    public Guid? FromBinId { get; set; }
    public string? FromBinCode { get; set; }
    public Guid? ToBinId { get; set; }
    public string? ToBinCode { get; set; }
    public int QuantityRequested { get; set; }
    public int QuantityDispatched { get; set; }
    public int QuantityReceived { get; set; }
    public int VarianceQuantity => Math.Max(0, QuantityDispatched - QuantityReceived);
    public Guid? VarianceAdjustmentId { get; set; }
    public AdjustmentStatus? VarianceAdjustmentStatus { get; set; }
    public string? VarianceAdjustmentStatusName => VarianceAdjustmentStatus?.ToString();
}

public class TransferResponseDto
{
    public Guid Id { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public Guid FromWarehouseId { get; set; }
    public string FromWarehouseName { get; set; } = string.Empty;
    public Guid ToWarehouseId { get; set; }
    public string ToWarehouseName { get; set; } = string.Empty;
    public Guid RequestedBy { get; set; }
    public string RequestedByUserName { get; set; } = string.Empty;
    public Guid? ApprovedBy { get; set; }
    public string? ApprovedByUserName { get; set; }
    public TransferStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? TransferDate { get; set; }
    public string? Notes { get; set; }
    public List<TransferItemResponseDto> Items { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public TransferVarianceResolutionStatus? VarianceResolutionStatus { get; set; }
    public string? VarianceResolutionStatusName => VarianceResolutionStatus?.ToString();
    public DateTime? VarianceResolvedAt { get; set; }
    public int PendingVarianceCount { get; set; }
    public int TotalVarianceQuantity { get; set; }
    public decimal TotalEstimatedLossValue { get; set; }
}

public class TransferQueryParameters : QueryParameters
{
    public Guid? FromWarehouseId { get; set; }
    public Guid? ToWarehouseId { get; set; }
    public Guid? WarehouseId { get; set; } // General warehouse filter (From OR To)
    public TransferStatus? Status { get; set; }
}

public class BinTransferCreateDto
{
    public Guid WarehouseId { get; set; }
    public Guid ProductId { get; set; }
    public Guid FromBinId { get; set; }
    public Guid ToBinId { get; set; }
    public int Quantity { get; set; }
    public string? Reason { get; set; }
    public bool BypassWarnings { get; set; }
    public string? OverrideReason { get; set; }
}
