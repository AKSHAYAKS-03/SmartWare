using SmartInventory.Core.Attributes;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Zone within a warehouse (Storage, Receiving, Shipping, etc.).
/// </summary>
public class WarehouseZone : BaseEntity, ISoftDelete
{
    [Sortable]
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public ZoneType ZoneType { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Capacity Limits
    public decimal AreaSqFt { get; set; }
    public decimal MaxVolumeCm3 { get; set; }
    public decimal MaxWeightKg { get; set; }

    // Foreign Keys
    public Guid WarehouseId { get; set; }

    // Navigation
    public Warehouse Warehouse { get; set; } = null!;
    public ICollection<BinLocation> BinLocations { get; set; } = [];
}
