using MediaButler.Core.Services;
using MediaButler.Data.Repositories;
using MediaButler.Services.BackgroundServices;
using MediaButler.Services.Domain;
using MediaButler.Services.EventHandlers;
using MediaButler.Services.Monitoring;
using Microsoft.Extensions.Configuration;
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
    /// Includes queue management, file discovery, and background service registration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The application configuration</param>
    /// <returns>The service collection for method chaining</returns>
    public static IServiceCollection AddBackgroundServices(this IServiceCollection services, IConfiguration configuration)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        // Configure file discovery settings
        services.Configure<FileDiscoveryConfiguration>(
            configuration.GetSection(FileDiscoveryConfiguration.SectionName));

        // Register file processing queue as singleton for shared state
        services.AddSingleton<IFileProcessingQueue, FileProcessingQueue>();
        
        // Register file discovery service as singleton
        services.AddSingleton<IFileDiscoveryService, FileDiscoveryService>();
        
        // Register processing coordinator as singleton
        services.AddSingleton<IProcessingCoordinator, ProcessingCoordinator>();
        
        // Register background services as hosted services
        services.AddHostedService<FileProcessingService>();
        services.AddHostedService<FileDiscoveryHostedService>();
        services.AddHostedService<ProcessingCoordinatorHostedService>();

        // Register processing log repository for audit trail
        services.AddScoped<IProcessingLogRepository, ProcessingLogRepository>();
        
        // Register domain event publisher
        services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();
        
        // Register event handlers for audit trail
        services.AddScoped<ProcessingLogEventHandler>();
        
        // Register concurrency handling services
        services.AddScoped<ConcurrencyHandler>();
        services.AddScoped<TransactionalFileService>();
        
        // Register metrics collection services
        services.AddSingleton<IMetricsCollectionService, MetricsCollectionService>();
        services.AddHostedService<MetricsMonitoringHostedService>();
        
        // Register metrics event handler for automatic metrics collection
        services.AddScoped<MetricsEventHandler>();
        
        // Register MediatR for domain event handling
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ProcessingLogEventHandler>());

        return services;
    }
}