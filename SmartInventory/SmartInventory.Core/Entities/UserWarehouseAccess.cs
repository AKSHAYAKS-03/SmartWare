using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Maps a user to a warehouse with a specific access level.
/// </summary>
public class UserWarehouseAccess : BaseEntity
{
    public AccessLevel AccessLevel { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    // Foreign Keys
    public Guid UserId { get; set; }
    public Guid WarehouseId { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
}
