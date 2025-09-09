using Microsoft.Extensions.DependencyInjection;

namespace MediaButler.Services.Background;

/// <summary>
/// Extension methods for registering background services in the dependency injection container.
/// Following "Simple Made Easy" principles with explicit, composable service registration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers background processing services for file processing operations.
    /// Includes queue management and background service registration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddBackgroundServices(this IServiceCollection services)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        // Register file processing queue as singleton for shared state
        services.AddSingleton<IFileProcessingQueue, FileProcessingQueue>();
        
        // Register background service as hosted service
        services.AddHostedService<FileProcessingService>();

        return services;
    }
}