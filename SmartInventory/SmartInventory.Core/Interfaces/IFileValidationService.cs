namespace SmartInventory.Core.Interfaces;

public interface IFileValidationService
{
    /// <summary>
    /// Validates the file stream against allowed magic numbers (file signatures).
    /// </summary>
    /// <param name="fileStream">The stream of the uploaded file.</param>
    /// <param name="fileName">The original file name.</param>
    /// <returns>True if the file is valid, false otherwise.</returns>
    bool IsValidFileSignature(Stream fileStream, string fileName);

    /// <summary>
    /// Validates if the file size is within the allowed limit.
    /// </summary>
    bool IsValidSize(long fileSizeBytes, long maxSizeBytes = 5 * 1024 * 1024); // Default 5MB
}
