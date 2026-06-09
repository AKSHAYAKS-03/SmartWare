using System.Threading.Tasks;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;

namespace SmartInventory.Core.Interfaces;
public interface ISupplierRepository : IGenericRepository<Supplier>
{
    Task<PagedResult<Supplier>> GetPagedSuppliersAsync(SupplierQueryParameters queryParams);
}
