using System.Threading.Tasks;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;

namespace SmartInventory.Core.Interfaces;


public interface IProductRepository : IGenericRepository<Product>
{
      Task<PagedResult<Product>> GetPagedProductsAsync(ProductQueryParameters queryParams);
}
