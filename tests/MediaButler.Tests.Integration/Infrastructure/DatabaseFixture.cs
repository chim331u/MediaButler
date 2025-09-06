using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MediaButler.Data;

namespace MediaButler.Tests.Integration.Infrastructure;

/// <summary>
/// Database fixture for integration tests using in-memory SQLite database.
/// Provides a fresh database instance per test class.
/// Follows "Simple Made Easy" principles by avoiding complex test container setup.
/// </summary>
public class DatabaseFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;
    
    public MediaButlerDbContext Context { get; }
    public IServiceProvider ServiceProvider => _serviceProvider;

    public DatabaseFixture()
    {
        // Create in-memory SQLite database connection
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Configure services for testing
        var services = new ServiceCollection();
        
        // Add Entity Framework with in-memory SQLite
        services.AddDbContext<MediaButlerDbContext>(options =>
        {
            options.UseSqlite(_connection);
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        });
        
        // Add logging for debugging  
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
        });
        
        // Add repository services (will be added when repositories are implemented)
        // services.AddScoped<IUnitOfWork, UnitOfWork>();
        // services.AddScoped<ITrackedFileRepository, TrackedFileRepository>();
        
        _serviceProvider = services.BuildServiceProvider();
        
        // Get context and ensure database is created
        Context = _serviceProvider.GetRequiredService<MediaButlerDbContext>();
        Context.Database.EnsureCreated();
    }

    /// <summary>
    /// Creates a new service scope for dependency injection in tests.
    /// Use this in tests that need fresh service instances.
    /// </summary>
    public IServiceScope CreateScope() => _serviceProvider.CreateScope();

    /// <summary>
    /// Cleans up all data from the database for test isolation.
    /// Call this between tests if sharing the same fixture instance.
    /// </summary>
    public async Task CleanupAsync()
    {
        // Remove all data in reverse dependency order
        Context.ProcessingLogs.RemoveRange(Context.ProcessingLogs);
        Context.TrackedFiles.RemoveRange(Context.TrackedFiles);
        Context.ConfigurationSettings.RemoveRange(Context.ConfigurationSettings);
        Context.UserPreferences.RemoveRange(Context.UserPreferences);
        
        await Context.SaveChangesAsync();
    }

    public void Dispose()
    {
        Context?.Dispose();
        _serviceProvider?.Dispose();
        _connection?.Dispose();
    }
}