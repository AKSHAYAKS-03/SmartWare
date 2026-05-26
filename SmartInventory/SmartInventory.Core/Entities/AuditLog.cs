namespace SmartInventory.Core.Entities;

/// <summary>
/// Audit trail for every create/update/delete action.
/// OldValues and NewValues stored as JSONB.
/// </summary>
public class AuditLog : BaseEntity
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty; // Create, Update, Delete
    public string? OldValues { get; set; } // JSONB
    public string? NewValues { get; set; } // JSONB
    public string? IpAddress { get; set; }

    // Foreign Keys
    public Guid UserId { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
