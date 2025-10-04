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
    private readonly IClassificationService _classificationService;
    private readonly ILogger<MLModelHealthCheck> _logger;

    /// <summary>
    /// Initializes a new instance of the MLModelHealthCheck.
    /// </summary>
    /// <param name="modelService">Service for ML model operations</param>
    /// <param name="predictionService">Service for prediction operations</param>
    /// <param name="classificationService">Service for classification operations</param>
    /// <param name="logger">Logger for health check operations</param>
    public MLModelHealthCheck(
        IMLModelService modelService,
        IPredictionService predictionService,
        IClassificationService classificationService,
        ILogger<MLModelHealthCheck> logger)
    {
        _modelService = modelService ?? throw new ArgumentNullException(nameof(modelService));
        _predictionService = predictionService ?? throw new ArgumentNullException(nameof(predictionService));
        _classificationService = classificationService ?? throw new ArgumentNullException(nameof(classificationService));
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

            // Determine ML implementation type
            var isRealML = _classificationService is MediaButler.ML.Services.RealClassificationService;
            var isMockML = _classificationService is MediaButler.ML.Services.ClassificationService;

            healthData["ml_implementation"] = isRealML ? "Real ML (with PredictionService)" :
                                              isMockML ? "Mock ML (temporary)" : "Unknown";
            healthData["implementation_type"] = _classificationService.GetType().Name;

            // Check if ML model is ready (for RealClassificationService)
            var isModelReady = _classificationService.IsModelReady();
            healthData["model_ready"] = isModelReady;
            healthData["model_status"] = isModelReady ? "loaded" : "not_loaded";

            if (isRealML && !isModelReady)
            {
                warnings.Add("Real ML implementation is active but model is not loaded. Using mock fallback.");
                warnings.Add("Train a model via /api/training/start to enable real ML predictions");
            }
            else if (isMockML)
            {
                warnings.Add("Using mock ML implementation. All predictions will return 'MOCK SERIES'");
                warnings.Add("This is a temporary implementation for testing purposes only");
            }

            // Check if model services are available
            try
            {
                // Test basic model availability by attempting a simple prediction
                var testResult = await _predictionService.PredictAsync("Test.File.mkv", cancellationToken);
                healthData["prediction_test"] = testResult.IsSuccess ? "passed" : "failed";

                if (testResult.IsSuccess)
                {
                    // Check if prediction result indicates mock or real ML
                    var isMockResult = testResult.Value.PredictedCategory == "MOCK SERIES";
                    healthData["prediction_result_type"] = isMockResult ? "mock_prediction" : "real_prediction";

                    if (isRealML && isMockResult)
                    {
                        healthData["using_fallback"] = true;
                        warnings.Add("Real ML is configured but currently using mock fallback (model not trained)");
                    }
                    else if (isRealML && !isMockResult)
                    {
                        healthData["using_fallback"] = false;
                        healthData["real_ml_active"] = true;
                    }
                }
                else
                {
                    warnings.Add($"Model prediction test failed: {testResult.Error}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Model availability check failed: {ex.Message}");
                healthData["prediction_test"] = "error";
            }

            // Additional detailed prediction test (only if model is ready)
            if (isModelReady)
            {
                try
                {
                    var detailedTestResult = await _predictionService.PredictAsync(
                        "The.Walking.Dead.S01E01.mkv",
                        cancellationToken);

                    if (detailedTestResult.IsSuccess)
                    {
                        healthData["detailed_prediction_test"] = "passed";
                        healthData["test_prediction_category"] = detailedTestResult.Value.PredictedCategory;
                        healthData["test_prediction_confidence"] = detailedTestResult.Value.Confidence;
                        healthData["test_prediction_time_ms"] = detailedTestResult.Value.ProcessingTimeMs;

                        // Verify this is actually a real ML prediction
                        if (detailedTestResult.Value.PredictedCategory != "MOCK SERIES")
                        {
                            healthData["verified_real_ml"] = true;
                        }
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