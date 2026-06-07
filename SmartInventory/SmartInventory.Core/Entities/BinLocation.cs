using SmartInventory.Core.Interfaces;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Specific bin slot within a warehouse zone.
/// </summary>
public class BinLocation : BaseEntity, ISoftDelete
{
    public string BinCode { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public bool IsActive { get; set; } = true;

    // ── Capacity Optimization Additions ───────────────────────────────────────
    
    public decimal MaxVolumeCm3 { get; set; } = 0;
    public decimal MaxWeightKg { get; set; } = 0;
    
    // Materialized columns for O(1) reads during putaway
    public decimal UtilizedVolumeCm3 { get; set; } = 0;
    public decimal UtilizedWeightKg { get; set; } = 0;
    
    public SmartInventory.Core.Enums.BinType BinType { get; set; } = SmartInventory.Core.Enums.BinType.Standard;

    // Foreign Keys
    public Guid ZoneId { get; set; }

    // Navigation
    public WarehouseZone Zone { get; set; } = null!;
    public ICollection<StockLevel> StockLevels { get; set; } = [];
}


// Suppose a product location is:

// Zone: Electronics
// BinCode: A2-R5-B12

// Meaning:

// Go to Electronics section
// → Find Bin A2-R5-B12