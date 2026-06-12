using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Repository.Repositories;


public class TransferRepository : GenericRepository<WarehouseTransfer>, ITransferRepository
{
    public TransferRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<WarehouseTransfer>> GetPagedTransfersAsync(TransferQueryParameters queryParams)
    {
        var query = _dbSet
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .AsQueryable();

        // search matching TransferNumber or Notes
        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            var searchPattern = queryParams.Search.Trim().ToLower();
            query = query.Where(t => t.TransferNumber.ToLower().Contains(searchPattern) 
                                  || (t.Notes != null && t.Notes.ToLower().Contains(searchPattern)));
        }

        // If a WarehouseId is supplied, it scopes the query such that transfers originating from OR destined for that location are shown.
        if (queryParams.WarehouseId.HasValue && queryParams.WarehouseId.Value != Guid.Empty)
        {
            var whId = queryParams.WarehouseId.Value;
            query = query.Where(t => t.FromWarehouseId == whId || t.ToWarehouseId == whId);
        }
        else
        {
            // Or individual filter params
            if (queryParams.FromWarehouseId.HasValue && queryParams.FromWarehouseId.Value != Guid.Empty)
            {
                query = query.Where(t => t.FromWarehouseId == queryParams.FromWarehouseId.Value);
            }

            if (queryParams.ToWarehouseId.HasValue && queryParams.ToWarehouseId.Value != Guid.Empty)
            {
                query = query.Where(t => t.ToWarehouseId == queryParams.ToWarehouseId.Value);
            }
        }

        if (queryParams.Status.HasValue)
        {
            query = query.Where(t => t.Status == queryParams.Status.Value);
        }
        int totalCount = await query.CountAsync();

        query = ApplySorting(query, queryParams.SortBy, queryParams.SortDir);

        int skip = (queryParams.Page - 1) * queryParams.PageSize;
        var data = await query.Skip(skip).Take(queryParams.PageSize).ToListAsync();

        return new PagedResult<WarehouseTransfer>
        {
            Data = data,
            TotalCount = totalCount,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    public async Task<WarehouseTransfer?> GetWithItemsAsync(Guid id)
    {
        return await _dbSet
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .Include(t => t.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(t => t.Id == id);
    }
}
