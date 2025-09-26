using MediaButler.Data.Repositories;

namespace MediaButler.Data.UnitOfWork;

/// <summary>
/// Unit of Work interface for coordinating repository operations within transactions.
/// Provides a simple, un-braided approach to transaction management following 
/// "Simple Made Easy" principles by keeping transaction concerns separate from repository logic.
/// </summary>
/// <remarks>
/// This interface ensures:
/// - Atomic operations across multiple repositories
/// - Consistent transaction boundaries
/// - Proper resource management through IDisposable
/// - Clear separation between transactional and non-transactional operations
/// - Simple coordination without complecting repository concerns
/// 
/// The Unit of Work pattern centralizes transaction management and ensures that
/// all repository operations within a unit of work share the same database context
/// and transaction scope.
/// </remarks>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Gets the TrackedFile repository for file management operations.
    /// All repository instances share the same database context and transaction scope.
    /// </summary>
    /// <value>The TrackedFile repository instance.</value>
    ITrackedFileRepository TrackedFiles { get; }

    /// <summary>
    /// Gets the ProcessingLog repository for audit trail operations.
    /// All repository instances share the same database context and transaction scope.
    /// </summary>
    /// <value>The ProcessingLog repository instance.</value>
    IRepository<MediaButler.Core.Entities.ProcessingLog> ProcessingLogs { get; }


    /// <summary>
    /// Gets the UserPreference repository for user preference management.
    /// All repository instances share the same database context and transaction scope.
    /// </summary>
    /// <value>The UserPreference repository instance.</value>
    IRepository<MediaButler.Core.Entities.UserPreference> UserPreferences { get; }

    /// <summary>
    /// Begins a new database transaction.
    /// All subsequent repository operations will be part of this transaction until
    /// CommitTransactionAsync or RollbackTransactionAsync is called.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A task representing the async operation.</returns>
    /// <remarks>
    /// Only one transaction can be active at a time per UnitOfWork instance.
    /// Calling this method when a transaction is already active will throw an exception.
    /// </remarks>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction, persisting all changes to the database.
    /// This operation is atomic - either all changes succeed or all are rolled back.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A task representing the async operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no transaction is active.</exception>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction, discarding all changes.
    /// This restores the database to its state before BeginTransactionAsync was called.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A task representing the async operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no transaction is active.</exception>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all pending changes to the database without using a transaction.
    /// This commits changes immediately and cannot be rolled back.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>The number of entities affected by the save operation.</returns>
    /// <remarks>
    /// Use this method for simple operations that don't require transaction coordination.
    /// For operations involving multiple repositories or requiring rollback capability,
    /// use BeginTransactionAsync/CommitTransactionAsync instead.
    /// </remarks>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value indicating whether a transaction is currently active.
    /// </summary>
    /// <value>True if a transaction is active, false otherwise.</value>
    bool HasActiveTransaction { get; }

    /// <summary>
    /// Creates a savepoint within the current transaction.
    /// Savepoints allow partial rollback to specific points within a transaction.
    /// </summary>
    /// <param name="savepointName">Unique name for the savepoint.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A task representing the async operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no transaction is active.</exception>
    /// <exception cref="ArgumentException">Thrown when savepoint name is null or empty.</exception>
    Task CreateSavepointAsync(string savepointName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back to a previously created savepoint within the current transaction.
    /// This discards changes made after the savepoint was created but keeps the transaction active.
    /// </summary>
    /// <param name="savepointName">Name of the savepoint to roll back to.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A task representing the async operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no transaction is active.</exception>
    /// <exception cref="ArgumentException">Thrown when savepoint name is null or empty.</exception>
    Task RollbackToSavepointAsync(string savepointName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a previously created savepoint.
    /// This is an optimization that frees resources but is not required for correctness.
    /// </summary>
    /// <param name="savepointName">Name of the savepoint to release.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>A task representing the async operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no transaction is active.</exception>
    /// <exception cref="ArgumentException">Thrown when savepoint name is null or empty.</exception>
    Task ReleaseSavepointAsync(string savepointName, CancellationToken cancellationToken = default);
}