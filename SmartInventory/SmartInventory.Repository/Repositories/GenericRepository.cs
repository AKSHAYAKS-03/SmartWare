using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;
using SmartInventory.Core.Exceptions;
using SmartInventory.Core.Interfaces;
using SmartInventory.Core.Attributes;
using SmartInventory.Repository.DynamicQueries;

namespace SmartInventory.Repository.Repositories;

/// <summary>
/// Generic repository implementing database access strategies using EF Core.
/// Developed to isolate all database manipulations and provide dynamic sorting and pagination at SQL translation time.
/// </summary>
/// <typeparam name="T">Target database entity derived from BaseEntity.</typeparam>
public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public GenericRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dbSet = _context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id)
    {
        return await _dbSet.FindAsync(id);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync(bool trackChanges = true)
    {
        return trackChanges ? await _dbSet.ToListAsync() : await _dbSet.AsNoTracking().ToListAsync();
    }

    public virtual IQueryable<T> Query(Expression<Func<T, bool>>? predicate = null, bool trackChanges = true)
    {
        var query = predicate != null ? _dbSet.Where(predicate) : _dbSet.AsQueryable();
        return trackChanges ? query : query.AsNoTracking();
    }

    public async Task<PagedResult<T>> GetPagedAsync(QueryParameters queryParams, Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        // 1. Apply predicate constraints if supplied
        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        // 3. Dynamic Whitelisted Sorting
        query = ApplySorting(query, queryParams.SortBy, queryParams.SortDir);

        // 2. Perform database-level count (optional)
        int totalCount = -1;
        if (!queryParams.SkipTotalCount)
        {
            totalCount = await query.CountAsync(cancellationToken);
        }

        // 4. PostgreSQL-driven paging window using Skip and Take
        int skip = (queryParams.Page - 1) * queryParams.PageSize;
        var data = await query.Skip(skip).Take(queryParams.PageSize).ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Data = data,
            TotalCount = totalCount,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    public virtual async Task<PagedResult<T>> GetPagedDynamicAsync(DynamicQueryRequest request, params Expression<Func<T, object>>[]? includes)
    {
        var query = _dbSet.AsQueryable();

        if (includes != null && includes.Length > 0)
        {
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            
            // ENTERPRISE FIX: Prevent Cartesian Explosion when multiple collections are included
            query = query.AsSplitQuery();
        }

        // 1. Apply advanced dynamic filtering securely via Expression Trees
        if (request.Filters != null && request.Filters.Any())
        {
            query = ExpressionBuilder.ApplyFilters(query, request.Filters);
        }

        // 2. Perform global search if required (basic implementation targeting Name or Description if they exist)
        if (!string.IsNullOrWhiteSpace(request.GlobalSearch))
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var nameProp = typeof(T).GetProperty("Name");
            if (nameProp != null)
            {
                var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
                var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                
                var memberAccess = Expression.MakeMemberAccess(parameter, nameProp);
                var lowerMember = Expression.Call(memberAccess, toLowerMethod!);
                
                var constant = Expression.Constant(request.GlobalSearch, typeof(string));
                var lowerConstant = Expression.Call(constant, toLowerMethod!);
                
                var call = Expression.Call(lowerMember, containsMethod!, lowerConstant);
                var lambda = Expression.Lambda<Func<T, bool>>(call, parameter);
                query = query.Where(lambda);
            }
        }

        // 3. Sorting
        if (request.Sorts != null && request.Sorts.Any())
        {
            var primarySort = request.Sorts.First();
            query = ApplySorting(query, primarySort.Field, primarySort.Direction);
        }
        else
        {
            query = ApplySorting(query, "createdAt", "desc");
        }

        // 4. Counting (Optional for performance)
        int totalCount = -1;
        if (!request.SkipTotalCount)
        {
            totalCount = await query.CountAsync();
        }

        // 5. Pagination
        int skip = (request.Page - 1) * request.PageSize;
        var data = await query.Skip(skip).Take(request.PageSize).ToListAsync();

        return new PagedResult<T>
        {
            Data = data,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public virtual async Task AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
    }

    public virtual void Update(T entity)
    {
        _dbSet.Attach(entity);
        _context.Entry(entity).State = EntityState.Modified;
    }

    public virtual void Delete(T entity)
    {
        // EF Core soft deletes are intercepted inside AppDbContext by checking if entity implements ISoftDelete.
        // Therefore, calling Delete here converts to setting IsActive = false if the interface is matched.
        _dbSet.Remove(entity);
    }

    /// <summary>
    /// Constructs a dynamic EF expression tree for sorting columns safely without string injections.
    /// </summary>
    protected IQueryable<T> ApplySorting(IQueryable<T> query, string sortBy, string sortDir)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return query.OrderByDescending(x => x.CreatedAt);
        }

        // Locate target column property case-insensitively
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = typeof(T).GetProperties()
            .FirstOrDefault(p => p.Name.Equals(sortBy, StringComparison.OrdinalIgnoreCase));

        // Fallback to CreatedAt if the requested column name is invalid
        if (property == null)
        {
            return sortDir.Equals("asc", StringComparison.OrdinalIgnoreCase)
                ? query.OrderBy(x => x.CreatedAt)
                : query.OrderByDescending(x => x.CreatedAt);
        }

        // Enterprise Restriction: Only allow sorting on columns marked with [Sortable]
        if (!Attribute.IsDefined(property, typeof(SortableAttribute)))
        {
            throw new ArgumentException($"Sorting by column '{property.Name}' is disabled for performance optimization.");
        }

        var memberAccess = Expression.MakeMemberAccess(parameter, property);
        var lambda = Expression.Lambda(memberAccess, parameter);

        string sortingMethod = sortDir.Equals("asc", StringComparison.OrdinalIgnoreCase) 
            ? "OrderBy" 
            : "OrderByDescending";

        var methodCallExpression = Expression.Call(
            typeof(Queryable),
            sortingMethod,
            new Type[] { typeof(T), property.PropertyType },
            query.Expression,
            Expression.Quote(lambda)
        );

        return query.Provider.CreateQuery<T>(methodCallExpression);
    }
}
