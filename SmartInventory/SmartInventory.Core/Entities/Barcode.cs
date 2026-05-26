using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;

/// <summary>
/// Barcode/QR code linked to a product.
/// </summary>
public class Barcode : BaseEntity
{
    public string BarcodeValue { get; set; } = string.Empty;
    public BarcodeType BarcodeType { get; set; }
    public bool IsPrimary { get; set; } = true;
    public string? ImagePath { get; set; }

    // Foreign Keys
    public Guid ProductId { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;
    public ICollection<BarcodeScanLog> ScanLogs { get; set; } = [];
}
