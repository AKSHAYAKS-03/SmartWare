using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Interfaces;

namespace SmartInventory.Repository.Repositories;

public class SupplierRepository : GenericRepository<Supplier>, ISupplierRepository
{
    public SupplierRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PagedResult<Supplier>> GetPagedSuppliersAsync(SupplierQueryParameters queryParams)
    {
        var query = _dbSet.AsQueryable();

        //  Text-based search matching Name, Code, Contact, or Email
        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            var searchPattern = queryParams.Search.Trim().ToLower();
            query = query.Where(s => s.Name.ToLower().Contains(searchPattern) 
                                  || s.Code.ToLower().Contains(searchPattern) 
                                  || (s.ContactPerson != null && s.ContactPerson.ToLower().Contains(searchPattern))
                                  || (s.Email != null && s.Email.ToLower().Contains(searchPattern)));
        }
        int totalCount = await query.CountAsync();

        query = ApplySorting(query, queryParams.SortBy, queryParams.SortDir);

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
