using SmartInventory.Core.Attributes;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Product category with self-referencing hierarchy (subcategories).
/// </summary>
public class Category : BaseEntity, ISoftDelete
{
    [Sortable]
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    // Foreign Keys (self-referencing)
    public Guid? ParentId { get; set; }

    // Navigation
    public Category? Parent { get; set; }
    public ICollection<Category> SubCategories { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
}



// Why Use Slugs?
// 1. Readability
// Better than IDs.
// Instead of:
// /warehouse/6ab7211
// Use:
// /warehouse/chennai-central