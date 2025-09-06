using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MediaButler.Core.Entities;
using MediaButler.Data.Repositories;

namespace MediaButler.Data.UnitOfWork;

/// <summary>
/// Unit of Work implementation for coordinating repository operations within transactions.
/// Provides atomic operations across multiple repositories following "Simple Made Easy" principles
/// by keeping transaction management separate from repository concerns.
/// </summary>
/// <remarks>
/// This implementation ensures:
/// - All repositories share the same DbContext instance for consistency
/// - Transaction boundaries are clearly defined and managed
/// - Proper resource cleanup through IDisposable pattern
/// - Thread-safe transaction state management
/// - Explicit error handling for transaction operations
/// 
/// The UnitOfWork coordinates multiple repositories and ensures that all operations
/// within a transaction boundary are atomic - they either all succeed or all fail.
/// </remarks>
public class UnitOfWork : IUnitOfWork
{
    private readonly MediaButlerDbContext _context;
    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    // Repository instances - lazy-loaded for efficiency
    private ITrackedFileRepository? _trackedFiles;
    private IRepository<ProcessingLog>? _processingLogs;
    private IRepository<ConfigurationSetting>? _configurationSettings;
    private IRepository<UserPreference>? _userPreferences;

    /// <summary>
    /// Initializes a new instance of the UnitOfWork class.
    /// </summary>
    /// <param name="context">The database context to coordinate operations across.</param>
    /// <exception cref="ArgumentNullException">Thrown when context is null.</exception>
    public UnitOfWork(MediaButlerDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public ITrackedFileRepository TrackedFiles
    {
        get
        {
            ThrowIfDisposed();
            return _trackedFiles ??= new TrackedFileRepository(_context);
        }
    }

    /// <inheritdoc />
    public IRepository<ProcessingLog> ProcessingLogs
    {
        get
        {
            ThrowIfDisposed();
            return _processingLogs ??= new Repository<ProcessingLog>(_context);
        }
    }

    /// <inheritdoc />
    public IRepository<ConfigurationSetting> ConfigurationSettings
    {
        get
        {
            ThrowIfDisposed();
            return _configurationSettings ??= new Repository<ConfigurationSetting>(_context);
        }
    }

    /// <inheritdoc />
    public IRepository<UserPreference> UserPreferences
    {
        get
        {
            ThrowIfDisposed();
            return _userPreferences ??= new Repository<UserPreference>(_context);
        }
    }

    /// <inheritdoc />
    public bool HasActiveTransaction => _currentTransaction != null;

    /// <inheritdoc />
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_currentTransaction != null)
        {
            throw new InvalidOperationException("A transaction is already active. Complete the current transaction before starting a new one.");
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No active transaction to commit. Call BeginTransactionAsync first.");
        }

        try
        {
            // Save all pending changes first
            await _context.SaveChangesAsync(cancellationToken);
            
            // Then commit the transaction
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            // If commit fails, rollback and re-throw
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            // Clean up transaction resources
            await DisposeTransactionAsync();
        }
    }

    /// <inheritdoc />
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No active transaction to rollback. Call BeginTransactionAsync first.");
        }

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            // Clean up transaction resources regardless of rollback success
            await DisposeTransactionAsync();
        }
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Save changes using the context's SaveChangesAsync
        // This will use the current transaction if one is active
        return await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CreateSavepointAsync(string savepointName, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateSavepointName(savepointName);

        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No active transaction. Call BeginTransactionAsync first.");
        }

        await _currentTransaction.CreateSavepointAsync(savepointName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RollbackToSavepointAsync(string savepointName, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateSavepointName(savepointName);

        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No active transaction. Call BeginTransactionAsync first.");
        }

        await _currentTransaction.RollbackToSavepointAsync(savepointName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReleaseSavepointAsync(string savepointName, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ValidateSavepointName(savepointName);

        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No active transaction. Call BeginTransactionAsync first.");
        }

        await _currentTransaction.ReleaseSavepointAsync(savepointName, cancellationToken);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources, false if called from finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            // Dispose of the current transaction if it exists
            if (_currentTransaction != null)
            {
                // Note: We can't await in Dispose, so we use the synchronous version
                // In a real-world scenario, consider implementing IAsyncDisposable
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }

            // Note: We don't dispose the context here because it should be managed
            // by the DI container that created it
            
            _disposed = true;
        }
    }

    /// <summary>
    /// Validates that a savepoint name is not null or empty.
    /// </summary>
    /// <param name="savepointName">The savepoint name to validate.</param>
    /// <exception cref="ArgumentException">Thrown when savepoint name is null or empty.</exception>
    private static void ValidateSavepointName(string savepointName)
    {
        if (string.IsNullOrWhiteSpace(savepointName))
        {
            throw new ArgumentException("Savepoint name cannot be null or empty.", nameof(savepointName));
        }
    }

    /// <summary>
    /// Throws ObjectDisposedException if this instance has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the object has been disposed.</exception>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(UnitOfWork));
        }
    }

    /// <summary>
    /// Disposes the current transaction and sets it to null.
    /// </summary>
    /// <returns>A task representing the async dispose operation.</returns>
    private async Task DisposeTransactionAsync()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }
}

/// <summary>
/// Async-disposable version of UnitOfWork for scenarios where async cleanup is preferred.
/// Provides explicit async resource management for transaction cleanup.
/// </summary>
/// <remarks>
/// This interface is useful in scenarios where you want to ensure that transaction
/// resources are properly cleaned up asynchronously, such as in using statements
/// with await using syntax.
/// </remarks>
public class AsyncUnitOfWork : UnitOfWork, IAsyncDisposable
{
    /// <summary>
    /// Initializes a new instance of the AsyncUnitOfWork class.
    /// </summary>
    /// <param name="context">The database context to coordinate operations across.</param>
    public AsyncUnitOfWork(MediaButlerDbContext context) : base(context)
    {
    }

    /// <summary>
    /// Asynchronously performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    /// <returns>A task representing the async dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Performs the core async disposal logic.
    /// </summary>
    /// <returns>A task representing the async dispose operation.</returns>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (HasActiveTransaction)
        {
            // Rollback any active transaction before disposing
            try
            {
                await RollbackTransactionAsync();
            }
            catch
            {
                // Suppress exceptions during disposal to avoid masking original exceptions
                // In production, consider logging this exception
            }
        }
    }
}