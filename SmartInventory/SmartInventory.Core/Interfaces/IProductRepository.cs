using System.Threading.Tasks;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;

namespace SmartInventory.Core.Interfaces;

/// <summary>
/// Specialized repository contract for Product entity operations.
/// Handles domain-specific querying like loading product variants, category relationships, and inventory thresholds.
/// </summary>
public interface IProductRepository : IGenericRepository<Product>
{
    /// <summary>
    /// Fetches a paginated and whitelisted set of products, including their Category and active stock sums.
    /// Scopes results to a warehouse if required by user role parameters.
    /// </summary>
    /// <param name="queryParams">Product-specific query parameters (Category, Low-stock flag, and Warehouse scope).</param>
    Task<PagedResult<Product>> GetPagedProductsAsync(ProductQueryParameters queryParams);
}
