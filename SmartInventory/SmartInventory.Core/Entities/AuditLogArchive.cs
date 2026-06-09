namespace SmartInventory.Core.Entities;


// Cold storage archive for audit log records older than 365 days.
//The nightly AuditLogArchiveJob moves records older than 1 year here in batches.

public class AuditLogArchive : BaseEntity
{
    public Guid? UserId { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? IpAddress { get; set; }

    public DateTime OriginalCreatedAt { get; set; }

    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
}
