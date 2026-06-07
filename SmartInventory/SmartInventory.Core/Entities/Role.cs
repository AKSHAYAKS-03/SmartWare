using SmartInventory.Core.Attributes;
namespace SmartInventory.Core.Entities;

/// <summary>
/// System role for RBAC (Admin, Manager, Staff, Viewer).
/// </summary>
public class Role : BaseEntity
{
    [Sortable]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // Using EF Core 8 primitive collections (JSON array mapping)
    public List<string> Permissions { get; set; } = [];

    // Navigation
    public ICollection<User> Users { get; set; } = [];
}
