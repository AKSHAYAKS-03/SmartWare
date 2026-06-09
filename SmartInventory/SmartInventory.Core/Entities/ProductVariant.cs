using SmartInventory.Core.Interfaces;

namespace SmartInventory.Core.Entities;

public class ProductVariant : BaseEntity, ISoftDelete
{
    public string VariantName { get; set; } = string.Empty;
    public string SkuSuffix { get; set; } = string.Empty;
    public string? Attributes { get; set; } // JSONB — e.g. {"color": "Red", "size": "L"}
    public bool IsActive { get; set; } = true;

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;
}
