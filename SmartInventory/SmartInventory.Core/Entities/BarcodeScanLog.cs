using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Entities;

public class BarcodeScanLog : BaseEntity
{
    public ScanAction Action { get; set; }
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;

    public Guid BarcodeId { get; set; }
    public Guid ScannedBy { get; set; }
    public Guid WarehouseId { get; set; }

    public Barcode Barcode { get; set; } = null!;
    public User ScannedByUser { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
}
