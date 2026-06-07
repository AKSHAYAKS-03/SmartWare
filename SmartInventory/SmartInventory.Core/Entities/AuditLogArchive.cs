namespace SmartInventory.Core.Entities;

/// <summary>
/// Cold storage archive for audit log records older than 365 days.
///
/// Business/Compliance rationale:
///   — The hot audit_logs table must remain small for query performance.
///   — Regulatory requirements in many jurisdictions mandate 7-year retention of audit trails.
///   — The nightly AuditLogArchiveJob moves records older than 1 year here in batches.
///   — This table is queryable (not deleted) — finance/compliance teams can still retrieve history.
///
/// Schema is intentionally identical to AuditLog to support transparent data access.
/// </summary>
public class AuditLogArchive : BaseEntity
{
    /// <summary>The user who performed the action.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Entity class name (e.g. "Product", "PurchaseOrder").</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Primary key of the affected entity.</summary>
    public Guid EntityId { get; set; }

    /// <summary>Action type: Create, Update, or Delete.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>JSON snapshot of the entity's state before the change (Update/Delete only).</summary>
    public string? OldValues { get; set; }

    /// <summary>JSON snapshot of the entity's state after the change (Create/Update only).</summary>
    public string? NewValues { get; set; }

    /// <summary>Client IP address at the time of the mutation — for security auditing.</summary>
    public string? IpAddress { get; set; }

    /// <summary>The date this record was originally created in the hot audit_logs table.</summary>
    public DateTime OriginalCreatedAt { get; set; }

    /// <summary>When this record was archived from the hot table to this cold storage table.</summary>
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
}
