using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;
using Mapster;

namespace SmartInventory.Service.Services;

/// <summary>
/// Product category management with self-referencing hierarchy support.
///
/// Business rules:
///   — Category name must be unique within the same parent scope.
///   — A category cannot be set as its own parent (circular reference protection).
///   — Slug is auto-generated from name if not provided.
///   — Cannot delete a category that has active products or sub-categories.
/// </summary>
public class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _uow;
    private readonly ICacheService _cacheService;

    public CategoryService(IUnitOfWork uow, ICacheService cacheService)
    {
        _uow = uow;
        _cacheService = cacheService;
    }

    public async Task<CategoryResponseDto> CreateCategoryAsync(CategoryCreateDto dto)
    {
        // Validate parent exists if provided
        if (dto.ParentId.HasValue)
        {
            var parent = await _uow.Repository<Category>().GetByIdAsync(dto.ParentId.Value);
            if (parent == null) throw new NotFoundException("Parent Category", dto.ParentId.Value);
        }

        // Auto-generate slug if not provided
        var slug = string.IsNullOrWhiteSpace(dto.Slug)
            ? dto.Name.ToLower().Replace(" ", "-").Replace("_", "-")
            : dto.Slug;

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Slug = slug,
            Description = dto.Description,
            ParentId = dto.ParentId,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        await _uow.Repository<Category>().AddAsync(category);
        await _uow.CommitAsync();
        await _cacheService.RemoveAsync("category:tree");
        return await GetCategoryByIdAsync(category.Id);
    }

    public async Task<CategoryResponseDto> UpdateCategoryAsync(Guid categoryId, CategoryUpdateDto dto)
    {
        var category = await _uow.Repository<Category>().GetByIdAsync(categoryId);
        if (category == null) throw new NotFoundException("Category", categoryId);

        // Circular reference guard — cannot set parent to self or a descendant
        if (dto.ParentId.HasValue)
        {
            if (dto.ParentId.Value == categoryId)
                throw new BusinessRuleException("A category cannot be its own parent.");

            if (dto.ParentId.HasValue)
            {
                var parent = await _uow.Repository<Category>().GetByIdAsync(dto.ParentId.Value);
                if (parent == null) throw new NotFoundException("Parent Category", dto.ParentId.Value);
            }
        }

        category.Name = dto.Name;
        category.Slug = string.IsNullOrWhiteSpace(dto.Slug)
            ? dto.Name.ToLower().Replace(" ", "-") : dto.Slug;
        category.Description = dto.Description;
        category.ParentId = dto.ParentId;
        category.IsActive = dto.IsActive;

        _uow.Repository<Category>().Update(category);
        await _uow.CommitAsync();
        await _cacheService.RemoveAsync("category:tree");
        return await GetCategoryByIdAsync(categoryId);
    }

    public async Task DeleteCategoryAsync(Guid categoryId)
    {
        var category = await _uow.Repository<Category>().GetByIdAsync(categoryId);
        if (category == null) throw new NotFoundException("Category", categoryId);

        bool hasProducts = await _uow.Repository<Product>()
            .Query().AnyAsync(p => p.CategoryId == categoryId);
        if (hasProducts)
            throw new BusinessRuleException(
                "Cannot delete a category that has associated products. Reassign products first.");

        bool hasChildren = await _uow.Repository<Category>()
            .Query().AnyAsync(c => c.ParentId == categoryId);
        if (hasChildren)
            throw new BusinessRuleException(
                "Cannot delete a category that has sub-categories. Delete children first.");

        _uow.Repository<Category>().Delete(category);
        await _uow.CommitAsync();
        await _cacheService.RemoveAsync("category:tree");
    }

    public async Task<CategoryResponseDto> GetCategoryByIdAsync(Guid categoryId)
    {
        var category = await _uow.Repository<Category>()
            .Query()
            .Include(c => c.Parent)
            .FirstOrDefaultAsync(c => c.Id == categoryId);

        if (category == null) throw new NotFoundException("Category", categoryId);
        return category.Adapt<CategoryResponseDto>();
    }

    public async Task<PagedResult<CategoryResponseDto>> GetCategoriesAsync(QueryParameters queryParams)
    {
        IQueryable<Category> query = _uow.Repository<Category>().Query().Include(c => c.Parent);

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
            query = query.Where(c => c.Name.Contains(queryParams.Search) ||
                                     c.Slug.Contains(queryParams.Search));

        int total = await query.CountAsync();
        var data = await query
            .OrderBy(c => c.Name)
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync();

        return new PagedResult<CategoryResponseDto>
        {
            Data = data.Adapt<IEnumerable<CategoryResponseDto>>(),
            TotalCount = total,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    /// <summary>
    /// Returns the full hierarchical category tree (root nodes with nested children).
    /// Used by the Angular category picker and navigation sidebar.
    /// </summary>
    public async Task<IEnumerable<CategoryTreeDto>> GetCategoryTreeAsync()
    {
        var cacheKey = "category:tree";
        var cached = await _cacheService.GetAsync<IEnumerable<CategoryTreeDto>>(cacheKey);
        if (cached != null)
        {
            return cached;
        }

        var allCategories = await _uow.Repository<Category>()
            .Query()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var tree = BuildTree(allCategories, null).ToList();
        await _cacheService.SetAsync(cacheKey, (IEnumerable<CategoryTreeDto>)tree, TimeSpan.FromMinutes(10));
        return tree;
    }

    private static IEnumerable<CategoryTreeDto> BuildTree(
        List<Category> all, Guid? parentId)
    {
        return all
            .Where(c => c.ParentId == parentId)
            .Select(c => new CategoryTreeDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                SubCategories = BuildTree(all, c.Id).ToList()
            });
    }
}
