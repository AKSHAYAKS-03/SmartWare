namespace SmartInventory.Core.Entities;

/// <summary>
/// Polymorphic file attachment. entity_type can be "product", "supplier", "purchase_order" etc.
/// </summary>
public class FileAttachment : BaseEntity
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    // Foreign Keys
    public Guid UploadedBy { get; set; }

    // Navigation
    public User UploadedByUser { get; set; } = null!;
}
