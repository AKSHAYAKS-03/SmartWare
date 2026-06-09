using SmartInventory.Core.Enums;
using SmartInventory.Core.Interfaces;
using SmartInventory.Core.Attributes;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Product in the inventory catalog.
/// Extended with Final_Plan features:
///   — ProductType (Raw/WIP/Finished/MRO) for operational prioritisation
///   — SafetyStockQty: emergency minimum before operations are affected
///   — AbcCategory: persisted result of the ABC classification engine (A/B/C)
/// </summary>
public class Product : BaseEntity, ISoftDelete
{
    [Sortable]
    public string Name { get; set; } = string.Empty;

    [Sortable]
    public string SKU { get; set; } = null!;

    public string? Description { get; set; }

    [Sortable]
    public UnitOfMeasure UnitOfMeasure { get; set; }

    [Sortable]
    public decimal CostPrice { get; set; }

    [Sortable]
    public decimal SellingPrice { get; set; }

    [Sortable]
    public int ReorderPoint { get; set; }

    [Sortable]
    public int ReorderQuantity { get; set; }

    [Sortable]
    public bool IsActive { get; set; } = true;
    public string? ImagePath { get; set; }

    // ── Final_Plan additions ──────────────────────────────────────────────────

    /// <summary>
    /// Classification of this product in the supply chain (Raw / WIP / Finished / MRO).
    /// Different types carry different business risk when stock runs low.
    /// </summary>
    public ProductType ProductType { get; set; } = ProductType.Finished;

    /// <summary>
    /// Safety stock quantity — the emergency buffer below which operations are at risk.
    /// Unlike ReorderPoint (which triggers ordering), safety stock is the absolute floor.
    /// </summary>
    public int SafetyStockQty { get; set; } = 0;

    /// <summary>
    /// Persisted result of the ABC inventory classification engine.
    /// Values: "A" (top 70% value), "B" (next 20%), "C" (bottom 10%).
    /// Null until the ABC engine has run at least once for this product.
    /// </summary>
    public string? AbcCategory { get; set; }

    // ── Capacity Optimization Additions ───────────────────────────────────────
    
    [Sortable]
    public decimal Length { get; set; } = 0;
    [Sortable]
    public decimal Width { get; set; } = 0;
    [Sortable]
    public decimal Height { get; set; } = 0;
    [Sortable]
    public decimal WeightKg { get; set; } = 0;
    
    /// <summary>
    /// Computed property for volumetric calculations during putaway (L x W x H).
    /// </summary>
    public decimal VolumeCm3 => Length * Width * Height;
    
    [Sortable]
    public BinType PreferredBinType { get; set; } = BinType.Standard;

    // ── Foreign Keys ─────────────────────────────────────────────────────────
    public Guid CategoryId { get; set; }

    // ── Navigation ───────────────────────────────────────────────────────────
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
