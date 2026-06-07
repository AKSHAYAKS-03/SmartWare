using System;
using Microsoft.AspNetCore.Http;
using SmartInventory.Core.Enums;

namespace SmartInventory.API.DTOs;

public class FileUploadRequest
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public DocumentCategory Category { get; set; } = DocumentCategory.General;
    public DateTime? ExpiryDate { get; set; }
    
    // The actual file from the multipart form
    public IFormFile File { get; set; } = null!;
}
