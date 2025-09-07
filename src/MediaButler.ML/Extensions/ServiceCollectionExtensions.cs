using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MediaButler.ML.Configuration;
using MediaButler.ML.Interfaces;

namespace MediaButler.ML.Extensions;

/// <summary>
/// Extension methods for registering ML services in dependency injection container.
/// This class provides a clean separation of ML registration concerns.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Only handles ML service registration
/// - No complecting: Separate from domain service registration
/// - Compose don't complex: Independent registration that can be composed
/// - Declarative: Clear service registration without implementation details
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds ML services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <param name="configuration">The configuration containing ML settings</param>
    /// <returns>The service collection for method chaining</returns>
    /// <remarks>
    /// This method registers:
    /// - ML configuration from appsettings
    /// - ML interfaces with their implementations (when implemented)
    /// - Background services for ML processing
    /// - Health checks for ML components
    /// </remarks>
    public static IServiceCollection AddMediaButlerML(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Register ML configuration
        services.Configure<MLConfiguration>(
            configuration.GetSection("MediaButler:ML"));

        // TODO: Register ML service implementations when created
        // services.AddScoped<ITokenizerService, TokenizerService>();
        // services.AddScoped<IClassificationService, ClassificationService>();
        // services.AddScoped<ITrainingDataService, TrainingDataService>();

        // TODO: Register background services when created
        // services.AddHostedService<MLProcessingBackgroundService>();

        // TODO: Register health checks when implemented
        // services.AddHealthChecks()
        //     .AddCheck<MLModelHealthCheck>("ml-model");

        return services;
    }

    /// <summary>
    /// Adds ML services with custom configuration.
    /// </summary>
    /// <param name="services">The service collection to add services to</param>
    /// <param name="configureOptions">Action to configure ML options</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddMediaButlerML(
        this IServiceCollection services, 
        Action<MLConfiguration> configureOptions)
    {
        // Register ML configuration with custom options
        services.Configure(configureOptions);

        // TODO: Register ML service implementations when created
        // services.AddScoped<ITokenizerService, TokenizerService>();
        // services.AddScoped<IClassificationService, ClassificationService>();
        // services.AddScoped<ITrainingDataService, TrainingDataService>();

        return services;
    }
}