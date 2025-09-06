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
        try
        {
            // Clear change tracker to avoid conflicts
            Context.ChangeTracker.Clear();
            
            // Remove all data using raw SQL to avoid tracking issues
            await Context.Database.ExecuteSqlRawAsync("DELETE FROM ProcessingLogs");
            await Context.Database.ExecuteSqlRawAsync("DELETE FROM TrackedFiles");
            await Context.Database.ExecuteSqlRawAsync("DELETE FROM ConfigurationSettings");
            await Context.Database.ExecuteSqlRawAsync("DELETE FROM UserPreferences");
            
            // Reset auto-increment counters for consistent test behavior
            try
            {
                await Context.Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name IN ('TrackedFiles', 'ProcessingLogs', 'ConfigurationSettings', 'UserPreferences')");
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
                // sqlite_sequence table doesn't exist yet - ignore error
            }
            
            // Clear change tracker again after cleanup
            Context.ChangeTracker.Clear();
        }
        catch (Exception)
        {
            // If cleanup fails, clear the change tracker at minimum
            Context.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>
    /// Seeds the database with test data for a specific scenario.
    /// Provides consistent test data setup for integration tests.
    /// </summary>
    public async Task SeedAsync(TestDataScenario scenario)
    {
        await CleanupAsync();
        
        switch (scenario)
        {
            case TestDataScenario.Workflow:
                await TestDataSeeder.SeedWorkflowScenarioAsync(Context);
                break;
            case TestDataScenario.Performance:
                await TestDataSeeder.SeedPerformanceTestDataAsync(Context, 100); // Smaller count for testing
                break;
            case TestDataScenario.SoftDelete:
                await TestDataSeeder.SeedSoftDeleteScenarioAsync(Context);
                break;
            case TestDataScenario.Classification:
                await TestDataSeeder.SeedClassificationTestDataAsync(Context);
                break;
            case TestDataScenario.Error:
                await TestDataSeeder.SeedErrorScenarioAsync(Context);
                break;
            case TestDataScenario.Minimal:
                await TestDataSeeder.SeedMinimalScenarioAsync(Context);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }
    }

    /// <summary>
    /// Creates a clean database state for each test method.
    /// Use this for tests that need guaranteed isolation.
    /// </summary>
    public async Task<DatabaseFixture> GetFreshInstanceAsync()
    {
        await CleanupAsync();
        return this;
    }

    public void Dispose()
    {
        Context?.Dispose();
        _serviceProvider?.Dispose();
        _connection?.Dispose();
    }
}