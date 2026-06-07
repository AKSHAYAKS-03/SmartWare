using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Repository.Repositories;

/// <summary>
/// Specialized PurchaseOrder repository implementing relational includes for items and suppliers.
/// Designed to support operational search and role-based warehouse isolation constraints.
/// </summary>
public class PurchaseOrderRepository : GenericRepository<PurchaseOrder>, IPurchaseOrderRepository
{
    public PurchaseOrderRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<PurchaseOrder>> GetPagedPurchaseOrdersAsync(PurchaseOrderQueryParameters queryParams)
    {
        var query = _dbSet
            .Include(po => po.Supplier)
            .Include(po => po.Warehouse)
            .AsQueryable();

        // 1. Search filter: search on PONumber or Notes
        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            var searchPattern = queryParams.Search.Trim().ToLower();
            query = query.Where(po => po.PoNumber.ToLower().Contains(searchPattern) 
                                  || (po.Notes != null && po.Notes.ToLower().Contains(searchPattern)));
        }

        // 2. Specific Supplier constraint
        if (queryParams.SupplierId.HasValue && queryParams.SupplierId.Value != Guid.Empty)
        {
            query = query.Where(po => po.SupplierId == queryParams.SupplierId.Value);
        }

        // 3. Warehouse scoping: restricts managers/staff to see only their assigned warehouse POs
        if (queryParams.WarehouseId.HasValue && queryParams.WarehouseId.Value != Guid.Empty)
        {
            query = query.Where(po => po.WarehouseId == queryParams.WarehouseId.Value);
        }

        // 4. Status filter
        if (queryParams.Status.HasValue)
        {
            query = query.Where(po => po.Status == queryParams.Status.Value);
        }

        // 5. DB count before fetch
        int totalCount = await query.CountAsync();

        // 6. DB sort
        query = ApplySorting(query, queryParams.SortBy, queryParams.SortDir);

        // 7. DB page skip
        int skip = (queryParams.Page - 1) * queryParams.PageSize;
        var data = await query.Skip(skip).Take(queryParams.PageSize).ToListAsync();

        return new PagedResult<PurchaseOrder>
        {
            Data = data,
            TotalCount = totalCount,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    public async Task<PurchaseOrder?> GetWithItemsAsync(Guid id)
    {
        return await _dbSet
            .Include(po => po.Supplier)
            .Include(po => po.Warehouse)
            .Include(po => po.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(po => po.Id == id);
    }
}
