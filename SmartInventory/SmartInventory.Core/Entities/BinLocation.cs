namespace SmartInventory.Core.Entities;

/// <summary>
/// Specific bin slot within a warehouse zone (Aisle-Rack-Bin).
/// </summary>
public class BinLocation : BaseEntity
{
    public string Aisle { get; set; } = string.Empty;
    public string Rack { get; set; } = string.Empty;
    public string Bin { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public bool IsActive { get; set; } = true;

    // Foreign Keys
    public Guid ZoneId { get; set; }

    // Navigation
    public WarehouseZone Zone { get; set; } = null!;
    public ICollection<StockLevel> StockLevels { get; set; } = [];
}


// Suppose a product location is:

// Zone: Electronics
// Aisle: A2
// Rack: R5
// Bin: B12

// Meaning:

// Go to Electronics section
// → Walk into Aisle A2
// → Find Rack R5
// → Product is in Bin B12