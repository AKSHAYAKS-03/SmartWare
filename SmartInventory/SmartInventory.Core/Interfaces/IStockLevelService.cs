using SmartInventory.Core.Enums;

namespace SmartInventory.Core.Interfaces;

public class AbcClassificationResultDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSKU { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalValue { get; set; }
    public decimal CumulativeValue { get; set; }
    public double CumulativePercentage { get; set; }
    public string Class { get; set; } = "C"; // A, B, or C
}

public interface IStockLevelService
{
    /// <summary>
    /// Computes the dynamic ABC classification for all inventory products.
    /// </summary>
    Task<List<AbcClassificationResultDto>> GetAbcClassificationAsync(Guid? warehouseId = null);

    /// <summary>
    /// Calculates the Economic Order Quantity (EOQ) for a product in a warehouse.
    /// </summary>
    Task<double> CalculateEoqAsync(Guid productId, Guid warehouseId);

    /// <summary>
    /// Calculates the stock value of a product in a warehouse using FIFO or Weighted Average method.
    /// </summary>
    Task<decimal> GetInventoryValuationAsync(Guid productId, Guid warehouseId, ValuationMethod method);
}
