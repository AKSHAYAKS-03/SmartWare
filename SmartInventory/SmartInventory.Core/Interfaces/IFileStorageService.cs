namespace SmartInventory.Core.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string fileName, string folderName, string? storagePrefix = null);

    Task DeleteFileAsync(string relativePath);

    bool FileExists(string relativePath);

    Task<Stream> GetFileStreamAsync(string relativePath);
}
