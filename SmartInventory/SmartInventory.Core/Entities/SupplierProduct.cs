namespace SmartInventory.Core.Entities;


public class SupplierProduct : BaseEntity
{
    public decimal UnitPrice { get; set; }
    public int LeadTimeDays { get; set; }
    public int MinOrderQuantity { get; set; }
    public bool IsPreferred { get; set; } = false;

    
    public bool IsActive { get; set; } = true;

    
    public Guid SupplierId { get; set; }
    public Guid ProductId { get; set; }

    
    public Supplier Supplier { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
