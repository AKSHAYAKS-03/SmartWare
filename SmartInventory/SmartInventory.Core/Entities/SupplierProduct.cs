namespace SmartInventory.Core.Entities;

/// <summary>
/// Maps a supplier to a product with pricing and order constraints.
/// </summary>
public class SupplierProduct : BaseEntity
{
    public decimal UnitPrice { get; set; }
    public int LeadTimeDays { get; set; }
    public int MinOrderQuantity { get; set; }
    public bool IsPreferred { get; set; } = false;

    // Foreign Keys
    public Guid SupplierId { get; set; }
    public Guid ProductId { get; set; }

    // Navigation
    public Supplier Supplier { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
