using SmartInventory.Core.Attributes;
using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Core.Entities;

public class WarehouseZone : BaseEntity, ISoftDelete
{
    [Sortable]
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = null!;
    public ZoneType ZoneType { get; set; }
    public bool IsActive { get; set; } = true;
    

    public decimal AreaSqFt { get; set; }
    public decimal MaxVolumeCm3 { get; set; }
    public decimal MaxWeightKg { get; set; }


    public Guid WarehouseId { get; set; }


    public Warehouse Warehouse { get; set; } = null!;
    public ICollection<BinLocation> BinLocations { get; set; } = [];
}
