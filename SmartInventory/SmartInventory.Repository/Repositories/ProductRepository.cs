using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Repository.Repositories;

/// <summary>
/// Specialized Product repository implementing core relational search models and warehouse-scoped queries.
/// </summary>
public class ProductRepository : GenericRepository<Product>, IProductRepository
{
    public ProductRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<Product>> GetPagedProductsAsync(ProductQueryParameters queryParams)
    {
        // Include related Category table
        var query = _dbSet
            .Include(p => p.Category)
            .AsQueryable();

        // 1. Text-based search on Product Name or SKU
        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            var searchPattern = queryParams.Search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(searchPattern) || p.SKU.ToLower().Contains(searchPattern));
        }

        // 2. Specific Category constraint
        if (queryParams.CategoryId.HasValue && queryParams.CategoryId.Value != Guid.Empty)
        {
            query = query.Where(p => p.CategoryId == queryParams.CategoryId.Value);
        }

        // 3. Low stock warning filter: evaluates whether the current cumulative warehouse inventory is at or below the product's threshold
        if (queryParams.LowStockOnly.HasValue && queryParams.LowStockOnly.Value)
        {
            query = query.Where(p => p.StockLevels.Sum(sl => sl.QuantityOnHand) <= p.ReorderPoint);
        }

        // 4. Warehouse security scoping: filters products that have active levels in the target warehouse
        if (queryParams.WarehouseId.HasValue && queryParams.WarehouseId.Value != Guid.Empty)
        {
            query = query.Where(p => p.StockLevels.Any(sl => sl.WarehouseId == queryParams.WarehouseId.Value));
        }

        // 5. Active state filter
        if (queryParams.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == queryParams.IsActive.Value);
        }

        // 6. DB-level total count
        int totalCount = await query.CountAsync();

        // 7. DB-level whitelisted sorting
        query = ApplySorting(query, queryParams.SortBy, queryParams.SortDir);

        // 8. SQL window extraction (Offset/Limit)
        int skip = (queryParams.Page - 1) * queryParams.PageSize;
        var data = await query.Skip(skip).Take(queryParams.PageSize).ToListAsync();

        return new PagedResult<Product>
        {
            Data = data,
            TotalCount = totalCount,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }
}
