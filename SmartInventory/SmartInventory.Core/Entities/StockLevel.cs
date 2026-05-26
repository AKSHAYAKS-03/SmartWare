namespace SmartInventory.Core.Entities;

/// <summary>
/// Current stock quantity per product per warehouse per bin.
/// </summary>
public class StockLevel : BaseEntity
{
    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }
    public int QuantityOnOrder { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    // Foreign Keys
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? BinLocationId { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public BinLocation? BinLocation { get; set; }
}
