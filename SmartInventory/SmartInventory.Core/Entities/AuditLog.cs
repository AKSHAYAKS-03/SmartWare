namespace SmartInventory.Core.Entities;


public class AuditLog : BaseEntity
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty; // Create, Update, Delete
    public string? OldValues { get; set; } 
    public string? NewValues { get; set; } 
    public string? IpAddress { get; set; }

    public Guid? UserId { get; set; }

    public User? User { get; set; }
}
