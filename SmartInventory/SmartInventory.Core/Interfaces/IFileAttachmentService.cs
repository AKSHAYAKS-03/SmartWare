using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SmartInventory.Core.DTOs;

namespace SmartInventory.Core.Interfaces;

public interface IFileAttachmentService
{
    Task<FileAttachmentResponseDto> UploadFileAsync(
        Stream fileStream, string fileName, string contentType, long fileLength, 
        string entityType, Guid entityId, SmartInventory.Core.Enums.DocumentCategory category, 
        DateTime? expiryDate, Guid uploadedBy);
    Task<FileAttachmentResponseDto> GetFileByIdAsync(Guid fileId);
    Task<IEnumerable<FileAttachmentResponseDto>> GetFilesByEntityAsync(string entityType, Guid entityId);
    Task DeleteFileAsync(Guid fileId, Guid performedBy);
    Task<Stream> GetFileStreamAsync(Guid fileId);
    Task<bool> VerifyFileAsync(Guid fileId, Guid verifiedBy);
}
