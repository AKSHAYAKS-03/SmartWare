using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Repository.Repositories;

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

        //  Search filter: Product Name, SKU, or Bin barcode
        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            var searchPattern = queryParams.Search.Trim().ToLower();
            query = query.Where(sl => sl.Product.Name.ToLower().Contains(searchPattern) 
                                   || sl.Product.SKU.ToLower().Contains(searchPattern)
                                   || (sl.BinLocation != null && sl.BinLocation.Barcode != null && sl.BinLocation.Barcode.ToLower().Contains(searchPattern)));
        }

        //  Specific Product constraint
        if (queryParams.ProductId.HasValue && queryParams.ProductId.Value != Guid.Empty)
        {
            query = query.Where(sl => sl.ProductId == queryParams.ProductId.Value);
        }

        //  Warehouse scoping: crucial for scoping Manager/Staff views to their home base
        if (queryParams.WarehouseId.HasValue && queryParams.WarehouseId.Value != Guid.Empty)
        {
            query = query.Where(sl => sl.WarehouseId == queryParams.WarehouseId.Value);
        }

        //  Specific Bin slot constraint
        if (queryParams.BinLocationId.HasValue && queryParams.BinLocationId.Value != Guid.Empty)
        {
            query = query.Where(sl => sl.BinLocationId == queryParams.BinLocationId.Value);
        }

      
        int totalCount = await query.CountAsync();

        
        query = ApplySorting(query, queryParams.SortBy, queryParams.SortDir);

    
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
