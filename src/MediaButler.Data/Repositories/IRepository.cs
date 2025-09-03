using System.Linq.Expressions;
using MediaButler.Core.Common;

namespace MediaButler.Data.Repositories;

/// <summary>
/// Generic repository interface for entities that inherit from BaseEntity.
/// This interface provides consistent CRUD operations with built-in soft delete support,
/// following "Simple Made Easy" principles with clear, un-braided concerns.
/// </summary>
/// <typeparam name="TEntity">The entity type that inherits from BaseEntity.</typeparam>
/// <remarks>
/// This repository interface ensures:
/// - Consistent data access patterns across all entities
/// - Built-in soft delete support through BaseEntity
/// - Separation of concerns between domain and infrastructure
/// - Explicit async/await patterns for scalability
/// - No complecting of query logic with entity behavior
/// </remarks>
public interface IRepository<TEntity> where TEntity : BaseEntity
{
    /// <summary>
    /// Retrieves an entity by its primary key.
    /// Respects global soft delete filters - only returns active entities.
    /// </summary>
    /// <param name="keyValues">The primary key values.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The entity if found and active, null otherwise.</returns>
    Task<TEntity?> GetByIdAsync(object[] keyValues, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an entity by its primary key, including soft-deleted entities.
    /// Use sparingly - primarily for audit and recovery scenarios.
    /// </summary>
    /// <param name="keyValues">The primary key values.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The entity if found (active or soft-deleted), null otherwise.</returns>
    Task<TEntity?> GetByIdIncludeDeletedAsync(object[] keyValues, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all active entities.
    /// Consider using GetPagedAsync for large datasets to avoid memory issues.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of all active entities.</returns>
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves entities matching the specified predicate.
    /// Only returns active entities due to global soft delete filters.
    /// </summary>
    /// <param name="predicate">Filter condition for entities.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Collection of entities matching the predicate.</returns>
    Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single entity matching the specified predicate.
    /// Throws exception if more than one entity matches.
    /// </summary>
    /// <param name="predicate">Filter condition for the entity.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The single entity matching the predicate, null if not found.</returns>
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paged collection of entities with optional filtering and sorting.
    /// Essential for performance when dealing with large datasets.
    /// </summary>
    /// <param name="skip">Number of entities to skip (for pagination).</param>
    /// <param name="take">Number of entities to take (page size).</param>
    /// <param name="predicate">Optional filter condition.</param>
    /// <param name="orderBy">Optional ordering function.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Paged collection of entities.</returns>
    Task<IEnumerable<TEntity>> GetPagedAsync(
        int skip, 
        int take, 
        Expression<Func<TEntity, bool>>? predicate = null,
        Expression<Func<TEntity, object>>? orderBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of active entities matching the optional predicate.
    /// </summary>
    /// <param name="predicate">Optional filter condition.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Count of matching entities.</returns>
    Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any active entities match the specified predicate.
    /// </summary>
    /// <param name="predicate">Filter condition.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>True if any entities match, false otherwise.</returns>
    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity to the repository.
    /// Note: Changes are not persisted until SaveChangesAsync is called.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <returns>The added entity (with potential modifications from database).</returns>
    TEntity Add(TEntity entity);

    /// <summary>
    /// Adds multiple entities to the repository.
    /// Note: Changes are not persisted until SaveChangesAsync is called.
    /// </summary>
    /// <param name="entities">The entities to add.</param>
    void AddRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Updates an existing entity in the repository.
    /// Automatically updates LastUpdateDate through BaseEntity.
    /// Note: Changes are not persisted until SaveChangesAsync is called.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <returns>The updated entity.</returns>
    TEntity Update(TEntity entity);

    /// <summary>
    /// Performs a soft delete on the entity by setting IsActive to false.
    /// The entity remains in the database but is excluded from normal queries.
    /// Note: Changes are not persisted until SaveChangesAsync is called.
    /// </summary>
    /// <param name="entity">The entity to soft delete.</param>
    /// <param name="reason">Optional reason for the deletion (stored in Note field).</param>
    void SoftDelete(TEntity entity, string? reason = null);

    /// <summary>
    /// Performs a hard delete on the entity, permanently removing it from the database.
    /// Use sparingly - prefer SoftDelete for audit trail maintenance.
    /// Note: Changes are not persisted until SaveChangesAsync is called.
    /// </summary>
    /// <param name="entity">The entity to permanently delete.</param>
    void HardDelete(TEntity entity);

    /// <summary>
    /// Restores a soft-deleted entity by setting IsActive to true.
    /// Note: Changes are not persisted until SaveChangesAsync is called.
    /// </summary>
    /// <param name="entity">The entity to restore.</param>
    /// <param name="reason">Optional reason for the restoration (stored in Note field).</param>
    void Restore(TEntity entity, string? reason = null);

    /// <summary>
    /// Persists all pending changes to the database.
    /// This includes all Add, Update, SoftDelete, HardDelete, and Restore operations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Number of entities affected by the save operation.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}