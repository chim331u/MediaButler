using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MediaButler.Data;

namespace MediaButler.Tests.Acceptance.Infrastructure;

/// <summary>
/// Web application factory for acceptance tests.
/// Provides a test server with in-memory database for end-to-end testing.
/// Follows "Simple Made Easy" principles by avoiding complex external dependencies.
/// </summary>
public class MediaButlerWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the production database registration
            var dbContextDescriptor = services.SingleOrDefault(d => 
                d.ServiceType == typeof(DbContextOptions<MediaButlerDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            var dbContextServiceDescriptor = services.SingleOrDefault(d => 
                d.ServiceType == typeof(MediaButlerDbContext));
            if (dbContextServiceDescriptor != null)
            {
                services.Remove(dbContextServiceDescriptor);
            }

            // Create in-memory SQLite database for testing
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Add test database context
            services.AddDbContext<MediaButlerDbContext>(options =>
            {
                options.UseSqlite(_connection);
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            });

            // Override logging for test environment
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Warning); // Reduce noise in tests
            });
        });

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Seeds the test database with initial data.
    /// Call this in test setup methods that need specific data scenarios.
    /// </summary>
    public async Task SeedDatabaseAsync(Action<MediaButlerDbContext> seedAction)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MediaButlerDbContext>();
        
        await context.Database.EnsureCreatedAsync();
        seedAction(context);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Cleans the test database.
    /// Use this between tests for isolation when sharing the same factory instance.
    /// </summary>
    public async Task CleanDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MediaButlerDbContext>();
        
        // Clear all tables in reverse dependency order
        context.ProcessingLogs.RemoveRange(context.ProcessingLogs);
        context.TrackedFiles.RemoveRange(context.TrackedFiles);
        context.ConfigurationSettings.RemoveRange(context.ConfigurationSettings);
        context.UserPreferences.RemoveRange(context.UserPreferences);
        
        await context.SaveChangesAsync();
    }
}