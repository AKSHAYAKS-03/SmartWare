using System.Collections.Generic;
using System.IO;
using System.Linq;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Service.Services;

public class FileValidationService : IFileValidationService
{
    // Common file signatures (Magic Numbers)
    private static readonly Dictionary<string, List<byte[]>> _fileSignatures = new()
    {
        { ".jpeg", new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, new byte[] { 0xFF, 0xD8, 0xFF, 0xE2 }, new byte[] { 0xFF, 0xD8, 0xFF, 0xE3 } } },
        { ".jpg", new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 }, new byte[] { 0xFF, 0xD8, 0xFF, 0xE8 } } },
        { ".png", new List<byte[]> { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
        { ".pdf", new List<byte[]> { new byte[] { 0x25, 0x50, 0x44, 0x46 } } },
        { ".csv", new List<byte[]> { } } // CSVs are plain text and don't have a reliable magic number, but we can accept them based on extension for now, or read content to verify it's printable text.
    };

    public bool IsValidFileSignature(Stream fileStream, string fileName)
    {
        if (fileStream == null || fileStream.Length == 0) return false;
        if (string.IsNullOrEmpty(fileName)) return false;

        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        if (string.IsNullOrEmpty(ext) || !_fileSignatures.ContainsKey(ext))
        {
            return false; // Extension not allowed
        }

        var signatures = _fileSignatures[ext];
        if (signatures.Count == 0)
        {
            // For files without reliable signatures like CSV/TXT, we just return true for the extension
            // In a highly secure environment, you'd scan the text content.
            return true;
        }

        // Read the header of the file to compare with signature
        using var reader = new BinaryReader(fileStream, System.Text.Encoding.UTF8, leaveOpen: true);
        var maxSignatureLength = signatures.Max(s => s.Length);
        
        fileStream.Position = 0; // Reset position to read header
        var headerBytes = reader.ReadBytes(maxSignatureLength);
        fileStream.Position = 0; // Reset position so caller can still read the file

        foreach (var signature in signatures)
        {
            if (headerBytes.Take(signature.Length).SequenceEqual(signature))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsValidSize(long fileSizeBytes, long maxSizeBytes = 5 * 1024 * 1024)
    {
        return fileSizeBytes > 0 && fileSizeBytes <= maxSizeBytes;
    }
}
