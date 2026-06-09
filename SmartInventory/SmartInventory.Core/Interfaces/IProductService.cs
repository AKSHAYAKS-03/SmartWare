using SmartInventory.Core.DTOs;

namespace SmartInventory.Core.Interfaces;

public interface IProductService
{
    Task<ProductResponseDto> CreateProductAsync(ProductCreateDto dto);
    Task<ProductResponseDto> UpdateProductAsync(Guid productId, ProductUpdateDto dto);
    Task DeleteProductAsync(Guid productId);
    Task<ProductResponseDto> GetProductByIdAsync(Guid productId);
    Task<PagedResult<ProductResponseDto>> GetProductsAsync(ProductQueryParameters queryParams);
    Task<PagedResult<ProductResponseDto>> SearchProductsAsync(DynamicQueryRequest request);
    Task<IEnumerable<ProductResponseDto>> GetLowStockProductsAsync(Guid? warehouseId = null);
    Task<IEnumerable<ProductResponseDto>> GetDeadStockProductsAsync(int daysThreshold = 90);
    Task UpdateAbcCategoriesAsync(Guid warehouseId);
}
