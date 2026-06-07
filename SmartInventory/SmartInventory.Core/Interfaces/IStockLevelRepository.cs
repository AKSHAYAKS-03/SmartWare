using System.Threading.Tasks;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;

namespace SmartInventory.Core.Interfaces;

/// <summary>
/// Specialized repository contract for StockLevel entity operations.
/// </summary>
public interface IStockLevelRepository : IGenericRepository<StockLevel>
{
    /// <summary>
    /// Fetches the stock level record for a specific product, warehouse, and bin location.
    /// </summary>
    Task<StockLevel?> GetStockLevelAsync(Guid productId, Guid warehouseId, Guid binLocationId);

    /// <summary>
    /// Fetches a paginated, sorted, and scoped catalog of physical stock levels.
    /// </summary>
    Task<PagedResult<StockLevel>> GetPagedStockLevelsAsync(StockLevelQueryParameters queryParams);
}
