using System.Threading.Tasks;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;

namespace SmartInventory.Core.Interfaces;

/// <summary>
/// Specialized repository contract for Supplier entity operations.
/// </summary>
public interface ISupplierRepository : IGenericRepository<Supplier>
{
    /// <summary>
    /// Fetches a paginated, sorted, and search-filtered collection of suppliers.
    /// </summary>
    Task<PagedResult<Supplier>> GetPagedSuppliersAsync(SupplierQueryParameters queryParams);
}
