using System.Threading.Tasks;
using SmartInventory.Core.Entities;

namespace SmartInventory.Core.Interfaces;

/// <summary>
/// Specialized repository contract for Barcode entity operations.
/// </summary>
public interface IBarcodeRepository : IGenericRepository<Barcode>
{
    /// <summary>
    /// Retrieves a barcode record with its associated product details by barcode value.
    /// </summary>
    Task<Barcode?> GetByValueAsync(string barcodeValue);
}
