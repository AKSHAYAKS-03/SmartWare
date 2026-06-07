namespace SmartInventory.Core.Interfaces;

public interface IFileStorageService
{
    /// <summary>
    /// Saves a file from a stream and returns its relative storage path.
    /// </summary>
    Task<string> SaveFileAsync(Stream fileStream, string fileName, string folderName);

    /// <summary>
    /// Deletes a file at the specified relative storage path.
    /// </summary>
    Task DeleteFileAsync(string relativePath);

    /// <summary>
    /// Checks if a file exists.
    /// </summary>
    bool FileExists(string relativePath);

    /// <summary>
    /// Returns a readable stream for the file at the specified relative storage path.
    /// </summary>
    Task<Stream> GetFileStreamAsync(string relativePath);
}
