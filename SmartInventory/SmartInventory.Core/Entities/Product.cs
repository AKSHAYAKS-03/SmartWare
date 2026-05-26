using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Product in the inventory catalog.
/// </summary>
public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string? Description { get; set; }
    public UnitOfMeasure UnitOfMeasure { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public int ReorderPoint { get; set; }
    public int ReorderQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ImagePath { get; set; }

    // Foreign Keys
    public Guid CategoryId { get; set; }

    // Navigation
    public Category Category { get; set; } = null!;
    public ICollection<ProductVariant> Variants { get; set; } = [];
    public ICollection<StockLevel> StockLevels { get; set; } = [];
    public ICollection<StockMovement> StockMovements { get; set; } = [];
    public ICollection<StockAdjustment> StockAdjustments { get; set; } = [];
    public ICollection<Barcode> Barcodes { get; set; } = [];
    public ICollection<SupplierProduct> SupplierProducts { get; set; } = [];
    public ICollection<AlertConfiguration> AlertConfigurations { get; set; } = [];
    public ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = [];
    public ICollection<TransferItem> TransferItems { get; set; } = [];
}
