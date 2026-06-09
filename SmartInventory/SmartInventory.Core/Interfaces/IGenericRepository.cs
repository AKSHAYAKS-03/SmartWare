using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;

namespace SmartInventory.Core.Interfaces;
public interface IGenericRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id);

    Task<IEnumerable<T>> GetAllAsync(bool trackChanges = true);

    IQueryable<T> Query(Expression<Func<T, bool>>? predicate = null, bool trackChanges = true);

    Task<PagedResult<T>> GetPagedAsync(QueryParameters queryParams, Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);

    Task<PagedResult<T>> GetPagedDynamicAsync(DynamicQueryRequest request, params Expression<Func<T, object>>[]? includes);

    Task AddAsync(T entity);

    void Update(T entity);

    void Delete(T entity);
}
