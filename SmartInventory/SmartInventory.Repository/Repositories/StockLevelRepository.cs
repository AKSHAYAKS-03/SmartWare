using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Repository.Repositories;

/// <summary>
/// Specialized StockLevel repository supporting exact slot lookups and warehouse security scopes.
/// </summary>
public class StockLevelRepository : GenericRepository<StockLevel>, IStockLevelRepository
{
    public StockLevelRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<StockLevel?> GetStockLevelAsync(Guid productId, Guid warehouseId, Guid binLocationId)
    {
        return await _dbSet
            .Include(sl => sl.Product)
            .Include(sl => sl.Warehouse)
            .Include(sl => sl.BinLocation)
            .FirstOrDefaultAsync(sl => sl.ProductId == productId 
                                    && sl.WarehouseId == warehouseId 
                                    && sl.BinLocationId == binLocationId);
    }

    public async Task<PagedResult<StockLevel>> GetPagedStockLevelsAsync(StockLevelQueryParameters queryParams)
    {
        var query = _dbSet
            .Include(sl => sl.Product)
            .Include(sl => sl.Warehouse)
            .Include(sl => sl.BinLocation)
            .AsQueryable();

        // 1. Search filter: Product Name, SKU, or Bin barcode
        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            var searchPattern = queryParams.Search.Trim().ToLower();
            query = query.Where(sl => sl.Product.Name.ToLower().Contains(searchPattern) 
                                   || sl.Product.SKU.ToLower().Contains(searchPattern)
                                   || (sl.BinLocation != null && sl.BinLocation.Barcode != null && sl.BinLocation.Barcode.ToLower().Contains(searchPattern)));
        }

        // 2. Specific Product constraint
        if (queryParams.ProductId.HasValue && queryParams.ProductId.Value != Guid.Empty)
        {
            query = query.Where(sl => sl.ProductId == queryParams.ProductId.Value);
        }

        // 3. Warehouse scoping: crucial for scoping Manager/Staff views to their home base
        if (queryParams.WarehouseId.HasValue && queryParams.WarehouseId.Value != Guid.Empty)
        {
            query = query.Where(sl => sl.WarehouseId == queryParams.WarehouseId.Value);
        }

        // 4. Specific Bin slot constraint
        if (queryParams.BinLocationId.HasValue && queryParams.BinLocationId.Value != Guid.Empty)
        {
            query = query.Where(sl => sl.BinLocationId == queryParams.BinLocationId.Value);
        }

        // Count
        int totalCount = await query.CountAsync();

        // Sort
        query = ApplySorting(query, queryParams.SortBy, queryParams.SortDir);

        // Page
        int skip = (queryParams.Page - 1) * queryParams.PageSize;
        var data = await query.Skip(skip).Take(queryParams.PageSize).ToListAsync();

        return new PagedResult<StockLevel>
        {
            Data = data,
            TotalCount = totalCount,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }
}
