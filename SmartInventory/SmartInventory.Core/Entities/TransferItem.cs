namespace SmartInventory.Core.Entities;

public class TransferItem : BaseEntity
{
    public int QuantityRequested { get; set; }
    public int QuantityDispatched { get; set; }
    public int QuantityReceived { get; set; }


    public Guid TransferId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? FromBinId { get; set; }
    public Guid? ToBinId { get; set; }


    public WarehouseTransfer Transfer { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public BinLocation? FromBin { get; set; }
    public BinLocation? ToBin { get; set; }
}
