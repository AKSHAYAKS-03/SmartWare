using SmartInventory.Core.Interfaces;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SmartInventory.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly string[] _allowedExtensions = { ".pdf", ".png", ".jpg", ".jpeg", ".webp", ".csv" };

    public LocalFileStorageService()
    {
        // By default, set the base path to "uploads" directory in the application root
        _basePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "uploads"));
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }

    private string GetSecureAbsolutePath(string relativePath)
    {
        var absolutePath = Path.GetFullPath(Path.Combine(_basePath, relativePath));
        
        // Jail/Sandbox Enforcement: Ensure the path hasn't escaped the base uploads directory
        if (!absolutePath.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("CRITICAL: Path traversal attempt blocked.");
        }
        return absolutePath;
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string folderName, string? storagePrefix = null)
    {
        var originalFileName = Path.GetFileName(fileName);
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (Array.IndexOf(_allowedExtensions, ext) < 0)
        {
            throw new UnauthorizedAccessException($"File extension {ext} is strictly forbidden.");
        }

        // Store files with a generated, filesystem-safe name. The original file
        // name is preserved separately in the database for download/display.
        var safePrefix = string.IsNullOrWhiteSpace(storagePrefix)
            ? string.Empty
            : new string(storagePrefix
                .Trim()
                .ToUpperInvariant()
                .Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                .ToArray());

        var generatedName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
        var storageFileName = string.IsNullOrWhiteSpace(safePrefix)
            ? generatedName
            : $"{safePrefix}_{generatedName}";
        var relativePath = Path.Combine(folderName, storageFileName);
        var absolutePath = GetSecureAbsolutePath(relativePath);

        var targetFolder = Path.GetDirectoryName(absolutePath);
        if (targetFolder != null && !Directory.Exists(targetFolder))
        {
            Directory.CreateDirectory(targetFolder);
        }

        using (var outputStream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await fileStream.CopyToAsync(outputStream);
        }

        return relativePath;
    }

    public Task DeleteFileAsync(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return Task.CompletedTask;

        var absolutePath = GetSecureAbsolutePath(relativePath);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    public bool FileExists(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return false;

        var absolutePath = GetSecureAbsolutePath(relativePath);
        return File.Exists(absolutePath);
    }

    public Task<Stream> GetFileStreamAsync(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new FileNotFoundException("File path is empty.");

        var absolutePath = GetSecureAbsolutePath(relativePath);

        if (!File.Exists(absolutePath))
            throw new FileNotFoundException($"File not found: {relativePath}", absolutePath);

        Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return Task.FromResult(stream);
    }
}
