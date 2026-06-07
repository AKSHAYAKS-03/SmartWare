using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Repository.Repositories;

/// <summary>
/// Specialized Supplier repository supporting full catalog filtering, lead time analysis, and search.
/// </summary>
public class SupplierRepository : GenericRepository<Supplier>, ISupplierRepository
{
    public SupplierRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<Supplier>> GetPagedSuppliersAsync(SupplierQueryParameters queryParams)
    {
        var query = _dbSet.AsQueryable();

        // 1. Text-based search matching Name, Code, Contact, or Email
        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            var searchPattern = queryParams.Search.Trim().ToLower();
            query = query.Where(s => s.Name.ToLower().Contains(searchPattern) 
                                  || s.Code.ToLower().Contains(searchPattern) 
                                  || (s.ContactPerson != null && s.ContactPerson.ToLower().Contains(searchPattern))
                                  || (s.Email != null && s.Email.ToLower().Contains(searchPattern)));
        }

        // 2. Count before extraction
        int totalCount = await query.CountAsync();

        // 3. Sorting
        query = ApplySorting(query, queryParams.SortBy, queryParams.SortDir);

        // 4. Paged Window
        int skip = (queryParams.Page - 1) * queryParams.PageSize;
        var data = await query.Skip(skip).Take(queryParams.PageSize).ToListAsync();

        return new PagedResult<Supplier>
        {
            Data = data,
            TotalCount = totalCount,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }
}
