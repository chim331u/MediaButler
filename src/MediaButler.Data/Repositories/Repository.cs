using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MediaButler.Core.Common;

namespace MediaButler.Data.Repositories;

/// <summary>
/// Generic repository implementation for entities that inherit from BaseEntity.
/// Provides consistent CRUD operations with built-in soft delete support,
/// following "Simple Made Easy" principles with explicit, un-braided functionality.
/// </summary>
/// <typeparam name="TEntity">The entity type that inherits from BaseEntity.</typeparam>
/// <remarks>
/// This implementation ensures:
/// - Consistent data access patterns without complecting concerns
/// - Leverages EF Core's global query filters for automatic soft delete handling
/// - Explicit async patterns for scalability without blocking threads
/// - Clear separation between repository logic and domain behavior
/// - No hidden complexities - each method does exactly what it declares
/// </remarks>
public class Repository<TEntity> : IRepository<TEntity> where TEntity : BaseEntity
{
    /// <summary>
    /// The database context for data access operations.
    /// </summary>
    protected readonly DbContext Context;
    
    /// <summary>
    /// The database set for the specific entity type.
    /// </summary>
    protected readonly DbSet<TEntity> DbSet;

    /// <summary>
    /// Initializes a new instance of the Repository class.
    /// </summary>
    /// <param name="context">The database context to use for data operations.</param>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    public Repository(DbContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        DbSet = Context.Set<TEntity>();
    }

    /// <inheritdoc />
    public virtual async Task<TEntity?> GetByIdAsync(object[] keyValues, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyValues);
        
        // EF Core's FindAsync respects global query filters (soft delete)
        return await DbSet.FindAsync(keyValues, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TEntity?> GetByIdIncludeDeletedAsync(object[] keyValues, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keyValues);
        
        // For entities with single primary key (most common case)
        if (keyValues.Length == 1)
        {
            var keyProperty = Context.Model.FindEntityType(typeof(TEntity))?.FindPrimaryKey()?.Properties.FirstOrDefault();
            if (keyProperty != null)
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(typeof(TEntity), "e");
                var property = System.Linq.Expressions.Expression.Property(parameter, keyProperty.Name);
                var value = System.Linq.Expressions.Expression.Constant(keyValues[0]);
                var equals = System.Linq.Expressions.Expression.Equal(property, value);
                var lambda = System.Linq.Expressions.Expression.Lambda<Func<TEntity, bool>>(equals, parameter);
                
                return await DbSet.IgnoreQueryFilters().FirstOrDefaultAsync(lambda, cancellationToken);
            }
        }
        
        // Fallback: Load all and filter in memory (not efficient but works for complex keys)
        var allEntities = await DbSet.IgnoreQueryFilters().ToListAsync(cancellationToken);
        return allEntities.FirstOrDefault(e =>
        {
            var entityKeyValues = Context.Entry(e).Properties
                .Where(p => p.Metadata.IsPrimaryKey())
                .Select(p => p.CurrentValue)
                .ToArray();
            return keyValues.SequenceEqual(entityKeyValues);
        });
    }

    /// <inheritdoc />
    public virtual async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        
        return await DbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        
        return await DbSet.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<IEnumerable<TEntity>> GetPagedAsync(
        int skip, 
        int take, 
        Expression<Func<TEntity, bool>>? predicate = null,
        Expression<Func<TEntity, object>>? orderBy = null,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0) throw new ArgumentException("Skip must be non-negative", nameof(skip));
        if (take <= 0) throw new ArgumentException("Take must be positive", nameof(take));

        var query = DbSet.AsQueryable();

        // Apply predicate filter if provided
        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        // Apply ordering if provided, otherwise order by CreatedDate for consistent pagination
        if (orderBy != null)
        {
            query = query.OrderBy(orderBy);
        }
        else
        {
            query = query.OrderBy(e => e.CreatedDate);
        }

        return await query.Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsQueryable();
        
        if (predicate != null)
        {
            query = query.Where(predicate);
        }
        
        return await query.CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        
        return await DbSet.AnyAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public virtual TEntity Add(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        
        var entry = DbSet.Add(entity);
        return entry.Entity;
    }

    /// <inheritdoc />
    public virtual void AddRange(IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        
        DbSet.AddRange(entities);
    }

    /// <inheritdoc />
    public virtual TEntity Update(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        
        // Mark entity as modified - BaseEntity will handle LastUpdateDate automatically
        entity.MarkAsModified();
        
        var entry = DbSet.Update(entity);
        return entry.Entity;
    }

    /// <inheritdoc />
    public virtual void SoftDelete(TEntity entity, string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        
        // Use BaseEntity's SoftDelete method for consistent behavior
        entity.SoftDelete(reason);
        
        // Mark entity as modified to ensure changes are tracked
        Context.Entry(entity).State = EntityState.Modified;
    }

    /// <inheritdoc />
    public virtual void HardDelete(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        
        DbSet.Remove(entity);
    }

    /// <inheritdoc />
    public virtual void Restore(TEntity entity, string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        
        // Use BaseEntity's Restore method for consistent behavior
        entity.Restore(reason);
        
        // Mark entity as modified to ensure changes are tracked
        Context.Entry(entity).State = EntityState.Modified;
    }

    /// <inheritdoc />
    public virtual async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // DbContext will automatically handle audit property updates through overridden SaveChangesAsync
        return await Context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Creates a query that includes related entities via navigation properties.
    /// Useful for derived repositories that need eager loading.
    /// </summary>
    /// <param name="includeProperties">Navigation property expressions to include.</param>
    /// <returns>Queryable with included properties.</returns>
    protected virtual IQueryable<TEntity> GetQueryWithIncludes(params Expression<Func<TEntity, object>>[] includeProperties)
    {
        IQueryable<TEntity> query = DbSet;
        
        foreach (var includeProperty in includeProperties)
        {
            query = query.Include(includeProperty);
        }
        
        return query;
    }

    /// <summary>
    /// Creates a query that ignores global query filters.
    /// Use sparingly - primarily for administrative and recovery operations.
    /// </summary>
    /// <returns>Queryable that includes soft-deleted entities.</returns>
    protected virtual IQueryable<TEntity> GetQueryIncludeDeleted()
    {
        return DbSet.IgnoreQueryFilters();
    }
}