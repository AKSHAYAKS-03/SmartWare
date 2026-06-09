using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;


public class Barcode : BaseEntity
{
    public string BarcodeValue { get; set; } = string.Empty;
    public BarcodeType BarcodeType { get; set; }
    public bool IsPrimary { get; set; } = true;
    public string? ImagePath { get; set; }

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;
    public ICollection<BarcodeScanLog> ScanLogs { get; set; } = [];
}
