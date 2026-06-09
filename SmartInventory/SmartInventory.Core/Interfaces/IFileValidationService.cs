namespace SmartInventory.Core.Interfaces;

public interface IFileValidationService
{    /// Validates the file stream against allowed magic numbers (file signatures).

    bool IsValidFileSignature(Stream fileStream, string fileName);

    bool IsValidSize(long fileSizeBytes, long maxSizeBytes = 5 * 1024 * 1024); // Default 5MB
}
