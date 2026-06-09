using SmartInventory.Core.Attributes;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Core.Entities;


public class Category : BaseEntity, ISoftDelete
{
    [Sortable]
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    // Foreign Keys (self-referencing)
    public Guid? ParentId { get; set; }

    public Category? Parent { get; set; }
    public ICollection<Category> SubCategories { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
}


