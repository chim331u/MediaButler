using MediaButler.ML.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace MediaButler.ML.Services;

/// <summary>
/// Background service that warms up the ML model on application startup.
/// This prevents first-request latency by pre-loading the FastText model into memory.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Only handles model warmup on startup
/// - No complecting: Independent from other background services
/// - Values over state: Stateless startup operation
/// - Non-blocking: Runs asynchronously without blocking application startup
///
/// Performance characteristics:
/// - Warmup time: 500-2000ms (model loading + test prediction)
/// - Memory impact: ~30-35MB (FastText model + runtime structures)
/// - Startup impact: Runs in background, doesn't block app startup
/// </remarks>
public class MLModelWarmupService : IHostedService
{
    private readonly ILogger<MLModelWarmupService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public MLModelWarmupService(
        ILogger<MLModelWarmupService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Starts the warmup process when the application starts.
    /// Runs asynchronously to avoid blocking application startup.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ML Model Warmup Service starting...");

        try
        {
            // Run warmup in background to avoid blocking application startup
            _ = Task.Run(async () =>
            {
                try
                {
                    // Wait a short delay to ensure all services are initialized
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("ML model warmup cancelled during initialization delay");
                        return;
                    }

                    _logger.LogInformation("Starting ML model warmup process...");
                    var startTime = DateTime.UtcNow;

                    // Create a scope to resolve the scoped service
                    using var scope = _serviceProvider.CreateScope();
                    var classificationService = scope.ServiceProvider.GetService<IClassificationService>();

                    if (classificationService is RealClassificationService realService)
                    {
                        var warmupSuccess = await realService.WarmupAsync();

                        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

                        if (warmupSuccess)
                        {
                            _logger.LogInformation(
                                "✅ ML model warmup completed successfully in {Duration}ms. System ready for real ML predictions.",
                                duration);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "⚠️ ML model warmup failed after {Duration}ms. System will use mock predictions until model is trained.",
                                duration);
                            _logger.LogInformation(
                                "To enable real ML predictions: 1) Train the model using /api/training/start endpoint, or 2) Import training data and train");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Classification service is not RealClassificationService. Warmup skipped.");
                        _logger.LogInformation("Current service type: {ServiceType}",
                            classificationService?.GetType().Name ?? "null");
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("ML model warmup was cancelled");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ML model warmup failed with exception. System will continue with mock predictions.");
                }
            }, cancellationToken);

            _logger.LogInformation("ML Model Warmup Service started (running in background)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start ML Model Warmup Service");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops the warmup service (no cleanup needed).
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ML Model Warmup Service stopping...");
        return Task.CompletedTask;
    }
}
