using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using MediaButler.ML.Interfaces;
using MediaButler.Core.Common;

namespace MediaButler.ML.HealthChecks;

/// <summary>
/// Health check for ML model availability and functionality.
/// Verifies that ML models are loaded and can perform basic operations.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Only checks ML model health
/// - Values over state: Immutable health check results
/// - Compose don't complect: Independent health check that can be composed with others
/// - Declarative: Clear health status without implementation details
/// </remarks>
public class MLModelHealthCheck : IHealthCheck
{
    private readonly IMLModelService _modelService;
    private readonly IPredictionService _predictionService;
    private readonly ILogger<MLModelHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the MLModelHealthCheck.
    /// </summary>
    /// <param name="modelService">Service for ML model operations</param>
    /// <param name="predictionService">Service for prediction operations</param>
    /// <param name="logger">Logger for health check operations</param>
    public MLModelHealthCheck(
        IMLModelService modelService,
        IPredictionService predictionService,
        ILogger<MLModelHealthCheck> logger)
    {
        _modelService = modelService ?? throw new ArgumentNullException(nameof(modelService));
        _predictionService = predictionService ?? throw new ArgumentNullException(nameof(predictionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Performs the ML model health check.
    /// </summary>
    /// <param name="context">Health check context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check result</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Starting ML model health check");

            var healthData = new Dictionary<string, object>();
            var warnings = new List<string>();
            var errors = new List<string>();

            // Check if model services are available  
            try
            {
                // Test basic model availability by attempting a simple prediction
                var testResult = await _predictionService.PredictAsync("Test.File.mkv", cancellationToken);
                healthData["model_status"] = testResult.IsSuccess ? "available" : "unavailable";
                if (!testResult.IsSuccess)
                {
                    warnings.Add($"Model prediction test failed: {testResult.Error}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Model availability check failed: {ex.Message}");
                healthData["model_status"] = "unavailable";
            }

            // Additional detailed prediction test
            if (healthData["model_status"]?.ToString() == "available")
            {
                try
                {
                    var detailedTestResult = await _predictionService.PredictAsync(
                        "The.Walking.Dead.S01E01.mkv", 
                        cancellationToken);

                    if (detailedTestResult.IsSuccess)
                    {
                        healthData["detailed_prediction_test"] = "passed";
                        healthData["test_prediction_confidence"] = detailedTestResult.Value.Confidence;
                        healthData["test_prediction_time_ms"] = detailedTestResult.Value.ProcessingTimeMs;
                    }
                    else
                    {
                        healthData["detailed_prediction_test"] = "failed";
                        warnings.Add($"Detailed test prediction failed: {detailedTestResult.Error}");
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"Detailed prediction test error: {ex.Message}");
                }
            }

            // Check performance metrics
            var performanceStats = await _predictionService.GetPerformanceStatsAsync();
            if (performanceStats.IsSuccess)
            {
                var avgTimeMs = performanceStats.Value.AveragePredictionTime.TotalMilliseconds;
                healthData["average_prediction_time_ms"] = Math.Round(avgTimeMs, 2);
                healthData["total_predictions"] = performanceStats.Value.TotalPredictions;
                healthData["success_rate"] = performanceStats.Value.SuccessRate;
                healthData["average_confidence"] = performanceStats.Value.AverageConfidence;

                // Check if performance is within acceptable limits (ARM32 target: <500ms)
                if (avgTimeMs > 500)
                {
                    warnings.Add($"Average prediction time ({avgTimeMs:F1}ms) exceeds ARM32 target of 500ms");
                }

                // Check if success rate is acceptable (target: >95%)
                if (performanceStats.Value.SuccessRate < 0.95)
                {
                    warnings.Add($"Prediction success rate ({performanceStats.Value.SuccessRate:P2}) below 95% target");
                }
            }
            else
            {
                warnings.Add("Performance statistics unavailable");
            }

            // Determine health status
            if (errors.Count > 0)
            {
                _logger.LogWarning("ML model health check failed with {ErrorCount} errors", errors.Count);
                return HealthCheckResult.Unhealthy(
                    description: $"ML model health check failed: {string.Join("; ", errors)}",
                    data: healthData);
            }

            if (warnings.Count > 0)
            {
                _logger.LogInformation("ML model health check completed with {WarningCount} warnings", warnings.Count);
                return HealthCheckResult.Degraded(
                    description: $"ML model operational with warnings: {string.Join("; ", warnings)}",
                    data: healthData);
            }

            _logger.LogDebug("ML model health check passed");
            return HealthCheckResult.Healthy(
                description: "ML model is operational and performing within acceptable parameters",
                data: healthData);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ML model health check was cancelled");
            return HealthCheckResult.Unhealthy("ML model health check was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ML model health check encountered an unexpected error");
            return HealthCheckResult.Unhealthy(
                description: $"ML model health check failed: {ex.Message}",
                exception: ex);
        }
    }
}