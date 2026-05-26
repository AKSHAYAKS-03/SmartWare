using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;

/// <summary>
/// In-app notification for a user.
/// </summary>
public class Notification : BaseEntity
{
    public NotificationChannel Channel { get; set; }
    public string Type { get; set; } = string.Empty; // e.g. "LowStock", "POApproved"
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public bool IsRead { get; set; } = false;

    // Foreign Keys
    public Guid UserId { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
