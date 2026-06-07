using SmartInventory.Core.Attributes;
using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Inter-warehouse transfer request with approval workflow.
/// </summary>
public class WarehouseTransfer : BaseEntity
{
    public string TransferNumber { get; set; } = null!;
    [Sortable]
    public TransferStatus Status { get; set; } = TransferStatus.Requested;
    public DateTime? TransferDate { get; set; }
    public string? Notes { get; set; }

    // Foreign Keys
    public Guid FromWarehouseId { get; set; }
    public Guid ToWarehouseId { get; set; }
    public Guid RequestedBy { get; set; }
    public Guid? ApprovedBy { get; set; }

    /// <summary>Set when transfer is received with transit variance.</summary>
    public TransferVarianceResolutionStatus? VarianceResolutionStatus { get; set; }
    public DateTime? VarianceResolvedAt { get; set; }

    // Navigation
    public Warehouse FromWarehouse { get; set; } = null!;
    public Warehouse ToWarehouse { get; set; } = null!;
    public User RequestedByUser { get; set; } = null!;
    public User? ApprovedByUser { get; set; }
    public ICollection<TransferItem> Items { get; set; } = [];
}
