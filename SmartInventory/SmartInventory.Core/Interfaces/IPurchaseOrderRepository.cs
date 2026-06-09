using System.Threading.Tasks;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;

namespace SmartInventory.Core.Interfaces;
public interface IPurchaseOrderRepository : IGenericRepository<PurchaseOrder>
{
    Task<PagedResult<PurchaseOrder>> GetPagedPurchaseOrdersAsync(PurchaseOrderQueryParameters queryParams);

    Task<PurchaseOrder?> GetWithItemsAsync(Guid id);
}
