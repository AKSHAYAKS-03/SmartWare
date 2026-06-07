using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using SmartInventory.Core.DTOs;
using SmartInventory.Core.Entities;

namespace SmartInventory.Core.Interfaces;

/// <summary>
/// Generic repository interface outlining core CRUD database actions.
/// Designed by the senior developer to unify access patterns and restrict direct EF dependencies to the Repository layer.
/// </summary>
/// <typeparam name="T">Base entity that carries a unique Guid identifier.</typeparam>
public interface IGenericRepository<T> where T : BaseEntity
{
    /// <summary>
    /// Retrieves a single record by its unique identifier.
    /// </summary>
    /// <param name="id">Unique identifier of the entity.</param>
    /// <returns>The entity if found; otherwise, null.</returns>
    Task<T?> GetByIdAsync(Guid id);

    /// <summary>
    /// Retrieves all active records in the database.
    /// Warning: Use with caution as this loads all records into application memory.
    /// </summary>
    Task<IEnumerable<T>> GetAllAsync(bool trackChanges = true);

    /// <summary>
    /// Exposes a database query builder (IQueryable) for LINQ filters and deferred query execution.
    /// Allows the service layer to append filtering/sorting criteria without pulling data early.
    /// </summary>
    /// <param name="predicate">Optional condition predicate to filter results.</param>
    IQueryable<T> Query(Expression<Func<T, bool>>? predicate = null, bool trackChanges = true);

    /// <summary>
    /// Generates a paginated, sorted, and filtered result from the database.
    /// All filtration and offset arithmetic execute directly in PostgreSQL.
    /// </summary>
    /// <param name="queryParams">Base pagination parameters containing page index, page size, search, and sort constraints.</param>
    /// <param name="predicate">Optional base condition predicate.</param>
    Task<PagedResult<T>> GetPagedAsync(QueryParameters queryParams, Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a dynamic search using enterprise-level advanced filtering and sorting.
    /// </summary>
    Task<PagedResult<T>> GetPagedDynamicAsync(DynamicQueryRequest request, params Expression<Func<T, object>>[]? includes);

    /// <summary>
    /// Adds a new entity record to the unit of work context tracking.
    /// </summary>
    Task AddAsync(T entity);

    /// <summary>
    /// Marks an entity as updated within the tracking context.
    /// </summary>
    void Update(T entity);

    /// <summary>
    /// Deletes an entity from database tracking.
    /// Note: If the entity implements ISoftDelete, this marks IsActive = false instead of a hard delete.
    /// </summary>
    void Delete(T entity);
}
