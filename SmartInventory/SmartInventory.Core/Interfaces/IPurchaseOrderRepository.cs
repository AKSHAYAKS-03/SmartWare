using System.Threading.Tasks;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;

namespace SmartInventory.Core.Interfaces;

/// <summary>
/// Specialized repository contract for PurchaseOrder entity operations.
/// </summary>
public interface IPurchaseOrderRepository : IGenericRepository<PurchaseOrder>
{
    /// <summary>
    /// Fetches a paginated set of purchase orders with optional supplier and warehouse scoping.
    /// </summary>
    Task<PagedResult<PurchaseOrder>> GetPagedPurchaseOrdersAsync(PurchaseOrderQueryParameters queryParams);

    /// <summary>
    /// Retrieves a purchase order including all its line items and details.
    /// </summary>
    Task<PurchaseOrder?> GetWithItemsAsync(Guid id);
}
