using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Zone within a warehouse (Storage, Receiving, Shipping, etc.).
/// </summary>
public class WarehouseZone : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public ZoneType ZoneType { get; set; }
    public bool IsActive { get; set; } = true;

    // Foreign Keys
    public Guid WarehouseId { get; set; }

    // Navigation
    public Warehouse Warehouse { get; set; } = null!;
    public ICollection<BinLocation> BinLocations { get; set; } = [];
}
