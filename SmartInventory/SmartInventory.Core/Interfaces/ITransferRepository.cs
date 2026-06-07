using System.Threading.Tasks;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;

namespace SmartInventory.Core.Interfaces;

/// <summary>
/// Specialized repository contract for WarehouseTransfer entity operations.
/// </summary>
public interface ITransferRepository : IGenericRepository<WarehouseTransfer>
{
    /// <summary>
    /// Fetches a paginated set of warehouse transfers, including from/to warehouse details.
    /// </summary>
    Task<PagedResult<WarehouseTransfer>> GetPagedTransfersAsync(TransferQueryParameters queryParams);

    /// <summary>
    /// Retrieves a transfer including its transfer items and product details.
    /// </summary>
    Task<WarehouseTransfer?> GetWithItemsAsync(Guid id);
}
