using SmartInventory.Core.Interfaces;

namespace SmartInventory.Core.Entities;

public class BinLocation : BaseEntity, ISoftDelete
{
    public string BinCode { get; set; } = null!;
    public string? Barcode { get; set; }
    public bool IsActive { get; set; } = true;

    
    public decimal MaxVolumeCm3 { get; set; } = 0;
    public decimal MaxWeightKg { get; set; } = 0;
    
    public decimal UtilizedVolumeCm3 { get; set; } = 0;
    public decimal UtilizedWeightKg { get; set; } = 0;
    
    public SmartInventory.Core.Enums.BinType BinType { get; set; } = SmartInventory.Core.Enums.BinType.Standard;

    public Guid ZoneId { get; set; }

    public WarehouseZone Zone { get; set; } = null!;
    public ICollection<StockLevel> StockLevels { get; set; } = [];
}

