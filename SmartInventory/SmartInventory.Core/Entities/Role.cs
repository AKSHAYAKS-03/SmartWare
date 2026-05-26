namespace SmartInventory.Core.Entities;

/// <summary>
/// System role for RBAC (Admin, Manager, Staff, Viewer).
/// </summary>
public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Navigation
    public ICollection<User> Users { get; set; } = [];
}
