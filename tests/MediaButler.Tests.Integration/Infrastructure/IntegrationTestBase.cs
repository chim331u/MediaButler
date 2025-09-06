using MediaButler.Data;
using Microsoft.Extensions.DependencyInjection;

namespace MediaButler.Tests.Integration.Infrastructure;

/// <summary>
/// Base class for integration tests providing database isolation and cleanup.
/// Ensures each test starts with a clean database state.
/// Follows "Simple Made Easy" principles with clear setup/teardown lifecycle.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    protected DatabaseFixture Fixture { get; }
    protected MediaButlerDbContext Context => Fixture.Context;
    protected IServiceProvider ServiceProvider => Fixture.ServiceProvider;

    protected IntegrationTestBase(DatabaseFixture fixture)
    {
        Fixture = fixture;
    }

    /// <summary>
    /// Called before each test method. Ensures clean database state.
    /// Override to provide custom setup while maintaining isolation.
    /// </summary>
    public virtual async Task InitializeAsync()
    {
        await Fixture.CleanupAsync();
    }

    /// <summary>
    /// Called after each test method. Provides cleanup opportunity.
    /// Default implementation does nothing - cleanup happens in InitializeAsync.
    /// </summary>
    public virtual Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Seeds the database with a specific test scenario.
    /// Convenience method for tests that need pre-configured data.
    /// </summary>
    protected async Task SeedDataAsync(TestDataScenario scenario)
    {
        await Fixture.SeedAsync(scenario);
    }

    /// <summary>
    /// Creates a new service scope for dependency injection.
    /// Use for tests that need fresh service instances.
    /// </summary>
    protected IServiceScope CreateScope() => Fixture.CreateScope();

    /// <summary>
    /// Asserts that the database is empty (clean state).
    /// Useful for verifying test isolation and cleanup.
    /// </summary>
    protected async Task AssertDatabaseIsEmptyAsync()
    {
        var trackedFileCount = Context.TrackedFiles.Count();
        var configCount = Context.ConfigurationSettings.Count();
        var logCount = Context.ProcessingLogs.Count();
        var userPrefCount = Context.UserPreferences.Count();

        if (trackedFileCount + configCount + logCount + userPrefCount > 0)
        {
            throw new InvalidOperationException(
                $"Database is not empty: TrackedFiles={trackedFileCount}, " +
                $"Configs={configCount}, Logs={logCount}, UserPrefs={userPrefCount}");
        }
    }

    /// <summary>
    /// Executes a test with automatic cleanup, ensuring isolation.
    /// Use for tests that might leave data behind despite proper cleanup.
    /// </summary>
    protected async Task<T> ExecuteIsolatedAsync<T>(Func<Task<T>> testAction)
    {
        try
        {
            await Fixture.CleanupAsync();
            return await testAction();
        }
        finally
        {
            await Fixture.CleanupAsync();
        }
    }

    /// <summary>
    /// Executes a test with automatic cleanup, ensuring isolation.
    /// Use for tests that might leave data behind despite proper cleanup.
    /// </summary>
    protected async Task ExecuteIsolatedAsync(Func<Task> testAction)
    {
        try
        {
            await Fixture.CleanupAsync();
            await testAction();
        }
        finally
        {
            await Fixture.CleanupAsync();
        }
    }
}