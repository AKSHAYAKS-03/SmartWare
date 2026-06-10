using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Service.Services;

public class FileAttachmentService : IFileAttachmentService
{
    private readonly IUnitOfWork _uow;
    private readonly IFileStorageService _storageService;
    private readonly IFileValidationService _validationService;

    public FileAttachmentService(IUnitOfWork uow, IFileStorageService storageService, IFileValidationService validationService)
    {
        _uow = uow;
        _storageService = storageService;
        _validationService = validationService;
    }

    public async Task<FileAttachmentResponseDto> UploadFileAsync(
        Stream fileStream, string fileName, string contentType, long fileLength, 
        string entityType, Guid entityId, SmartInventory.Core.Enums.DocumentCategory category, 
        DateTime? expiryDate, Guid uploadedBy)
    {
        if (fileStream == null || fileLength == 0)
            throw new BusinessRuleException("No file provided.");

        if (!_validationService.IsValidSize(fileLength))
            throw new BusinessRuleException("File size exceeds the allowed limit of 5MB.");

        if (!_validationService.IsValidFileSignature(fileStream, fileName))
            throw new BusinessRuleException("File signature validation failed. The file type is not permitted or the content is spoofed.");

        string folderName = entityType.ToLower();
        string relativePath;

        relativePath = await _storageService.SaveFileAsync(fileStream, fileName, folderName, entityType);

        var attachment = new FileAttachment
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            FileName = fileName,
            FilePath = relativePath,
            MimeType = contentType,
            FileSizeBytes = fileLength,
            Category = category,
            ExpiryDate = expiryDate,
            UploadedBy = uploadedBy,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<FileAttachment>().AddAsync(attachment);
        await _uow.CommitAsync();

        return MapToDto(attachment);
    }

    public async Task<FileAttachmentResponseDto> GetFileByIdAsync(Guid fileId)
    {
        var file = await _uow.Repository<FileAttachment>().GetByIdAsync(fileId);
        if (file == null)
            throw new NotFoundException("FileAttachment", fileId);

        return MapToDto(file);
    }

    public async Task<IEnumerable<FileAttachmentResponseDto>> GetFilesByEntityAsync(string entityType, Guid entityId)
    {
        var files = await _uow.Repository<FileAttachment>()
            .Query()
            .Where(f => f.EntityType == entityType && f.EntityId == entityId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return files.Select(MapToDto);
    }

    public async Task DeleteFileAsync(Guid fileId, Guid performedBy)
    {
        var file = await _uow.Repository<FileAttachment>().GetByIdAsync(fileId);
        if (file == null)
            throw new NotFoundException("FileAttachment", fileId);

        // Delete from storage
        if (_storageService.FileExists(file.FilePath))
        {
            await _storageService.DeleteFileAsync(file.FilePath);
        }

        // Delete from DB (Hard delete or soft delete, FileAttachment currently inherits BaseEntity but not ISoftDelete)
        _uow.Repository<FileAttachment>().Delete(file);
        await _uow.CommitAsync();
    }

    public async Task<Stream> GetFileStreamAsync(Guid fileId)
    {
        var file = await _uow.Repository<FileAttachment>().GetByIdAsync(fileId);
        if (file == null)
            throw new NotFoundException("FileAttachment", fileId);

        if (!_storageService.FileExists(file.FilePath))
            throw new NotFoundException("File Storage", fileId);

        return await _storageService.GetFileStreamAsync(file.FilePath);
    }

    public async Task<bool> VerifyFileAsync(Guid fileId, Guid verifiedBy)
    {
        var file = await _uow.Repository<FileAttachment>().GetByIdAsync(fileId);
        if (file == null)
            throw new NotFoundException("FileAttachment", fileId);

        file.IsVerified = true;
        file.VerifiedBy = verifiedBy;
        file.UpdatedAt = DateTime.UtcNow;

        _uow.Repository<FileAttachment>().Update(file);
        await _uow.CommitAsync();

        return true;
    }

    private FileAttachmentResponseDto MapToDto(FileAttachment file)
    {
        return new FileAttachmentResponseDto
        {
            Id = file.Id,
            EntityType = file.EntityType,
            EntityId = file.EntityId,
            FileName = file.FileName,
            FilePath = file.FilePath,
            MimeType = file.MimeType,
            FileSizeBytes = file.FileSizeBytes,
            Category = file.Category,
            ExpiryDate = file.ExpiryDate,
            IsVerified = file.IsVerified,
            VerifiedBy = file.VerifiedBy,
            UploadedBy = file.UploadedBy,
            CreatedAt = file.CreatedAt
        };
    }
}
