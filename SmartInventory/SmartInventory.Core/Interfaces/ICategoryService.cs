using SmartInventory.Core.DTOs;

namespace SmartInventory.Core.Interfaces;
public interface ICategoryService
{
    Task<CategoryResponseDto> CreateCategoryAsync(CategoryCreateDto dto);
    Task<CategoryResponseDto> UpdateCategoryAsync(Guid categoryId, CategoryUpdateDto dto);
    Task DeleteCategoryAsync(Guid categoryId);
    Task<CategoryResponseDto> GetCategoryByIdAsync(Guid categoryId);
    Task<PagedResult<CategoryResponseDto>> GetCategoriesAsync(QueryParameters queryParams);
    Task<IEnumerable<CategoryTreeDto>> GetCategoryTreeAsync();
}
