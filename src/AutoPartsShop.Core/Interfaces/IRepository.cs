using System.Linq.Expressions;

namespace AutoPartsShop.Core.Interfaces;

/// <summary>
/// Generic repository contract for aggregate root entities.
/// Provides standard CRUD operations and querying capabilities.
/// </summary>
/// <typeparam name="T">The entity type managed by this repository.</typeparam>
public interface IRepository<T> where T : Entities.BaseEntity
{
    /// <summary>
    /// Gets an entity by its identifier.
    /// </summary>
    /// <param name="id">The entity identifier.</param>
    /// <returns>The entity if found; otherwise, <c>null</c>.</returns>
    Task<T?> GetByIdAsync(int id);

    /// <summary>
    /// Gets all entities of this type.
    /// </summary>
    /// <returns>A read-only list of all entities.</returns>
    Task<IReadOnlyList<T>> GetAllAsync();

    /// <summary>
    /// Finds entities matching the given specification.
    /// </summary>
    /// <param name="specification">The specification criteria to match.</param>
    /// <returns>A read-only list of matching entities.</returns>
    Task<IReadOnlyList<T>> FindAsync(ISpecification<T> specification);

    /// <summary>
    /// Finds entities matching the given specification with tracking enabled (for updates).
    /// </summary>
    /// <param name="specification">The specification criteria to match.</param>
    /// <returns>A read-only list of matching entities with change tracking.</returns>
    Task<IReadOnlyList<T>> FindTrackedAsync(ISpecification<T> specification);

    /// <summary>
    /// Adds a new entity to the repository.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <returns>The added entity.</returns>
    Task<T> AddAsync(T entity);

    /// <summary>
    /// Updates an existing entity in the repository.
    /// </summary>
    /// <param name="entity">The entity with updated values.</param>
    Task UpdateAsync(T entity);

    /// <summary>
    /// Deletes an entity from the repository.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    Task DeleteAsync(T entity);

    /// <summary>
    /// Returns the total count of entities.
    /// </summary>
    Task<int> CountAsync();

    /// <summary>
    /// Gets a paged subset of entities.
    /// </summary>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <returns>A read-only list of entities for the requested page.</returns>
    Task<IReadOnlyList<T>> GetPagedAsync(int page, int pageSize);
}
