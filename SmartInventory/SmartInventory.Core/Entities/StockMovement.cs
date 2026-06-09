using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;


public class StockMovement : BaseEntity
{
    public MovementType MovementType { get; set; }
    public int Quantity { get; set; }
    public ReferenceType ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }

    // Foreign Keys
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? BinLocationId { get; set; }
    public Guid PerformedBy { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public BinLocation? BinLocation { get; set; }
    public User PerformedByUser { get; set; } = null!;
}
