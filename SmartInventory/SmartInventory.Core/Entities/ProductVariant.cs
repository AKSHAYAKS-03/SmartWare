using SmartInventory.Core.Interfaces;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Product variant (e.g., Red-Large). Attributes stored as JSONB.
/// </summary>
public class ProductVariant : BaseEntity, ISoftDelete
{
    public string VariantName { get; set; } = string.Empty;
    public string SkuSuffix { get; set; } = string.Empty;
    public string? Attributes { get; set; } // JSONB — e.g. {"color": "Red", "size": "L"}
    public bool IsActive { get; set; } = true;

    // Foreign Keys
    public Guid ProductId { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;
}
