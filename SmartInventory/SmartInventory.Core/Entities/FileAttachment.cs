using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;


public class FileAttachment : BaseEntity
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    
    public DocumentCategory Category { get; set; } = DocumentCategory.General;
    public DateTime? ExpiryDate { get; set; }
    public bool IsVerified { get; set; } = false;
    public Guid? VerifiedBy { get; set; }

    public Guid UploadedBy { get; set; }

    public User UploadedByUser { get; set; } = null!;
}
