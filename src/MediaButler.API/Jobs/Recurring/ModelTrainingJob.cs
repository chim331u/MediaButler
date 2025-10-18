using Hangfire;
using MediaButler.Services.ML;
using Microsoft.Extensions.Logging;

namespace MediaButler.API.Jobs.Recurring;

/// <summary>
/// Hangfire recurring job for ML model training.
/// Retrains classification model weekly using database training data.
/// Optimized for ARM32 with concurrent execution disabled and extended timeout.
/// </summary>
[Queue("low-priority")]
[AutomaticRetry(Attempts = 1, DelaysInSeconds = new[] { 300 })]  // Retry once after 5 min
[DisableConcurrentExecution(1800)]  // 30 min max, no concurrent runs
public class ModelTrainingJob
{
    private readonly IDatabaseTrainingService _trainingService;
    private readonly ILogger<ModelTrainingJob> _logger;

    public ModelTrainingJob(
        IDatabaseTrainingService trainingService,
        ILogger<ModelTrainingJob> logger)
    {
        _trainingService = trainingService ?? throw new ArgumentNullException(nameof(trainingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Trains ML model from database training data.
    /// Runs weekly on Sunday at 3:00 AM by default.
    /// </summary>
    [JobDisplayName("ML Model Training - Weekly Retraining")]
    public async Task TrainModelAsync()
    {
        _logger.LogInformation("Weekly ML model training job started");

        try
        {
            var sessionId = $"weekly-training-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            var result = await _trainingService.TrainModelFromDatabaseAsync(sessionId, CancellationToken.None);

            if (result.IsSuccess)
            {
                var trainingResult = result.Value;
                _logger.LogInformation(
                    "Weekly ML training completed successfully. " +
                    "Model Version: {Version}, " +
                    "Accuracy: {Accuracy:P2}, " +
                    "Training Samples: {TrainingSamples}, " +
                    "Duration: {Duration}s",
                    trainingResult.ModelVersion,
                    trainingResult.ValidationMetrics.Accuracy,
                    trainingResult.TrainingSampleCount,
                    trainingResult.TrainingDuration.TotalSeconds);

                // Log per-category metrics for detailed analysis
                _logger.LogDebug("Per-category metrics:");
                foreach (var categoryMetric in trainingResult.ValidationMetrics.PerCategoryMetrics)
                {
                    _logger.LogDebug(
                        "  Category '{Category}': Precision={Precision:P2}, Recall={Recall:P2}, F1={F1:P2}",
                        categoryMetric.Value.CategoryName,
                        categoryMetric.Value.Precision,
                        categoryMetric.Value.Recall,
                        categoryMetric.Value.F1Score);
                }

                // Log overall training metrics
                _logger.LogInformation(
                    "Training metrics - MacroF1: {MacroF1:P2}, WeightedF1: {WeightedF1:P2}, LogLoss: {LogLoss:F4}",
                    trainingResult.ValidationMetrics.MacroF1Score,
                    trainingResult.ValidationMetrics.WeightedF1Score,
                    trainingResult.ValidationMetrics.LogLoss);
            }
            else
            {
                _logger.LogError("Weekly ML training failed: {Error}", result.Error);
                throw new InvalidOperationException($"Model training failed: {result.Error}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Weekly ML model training job failed with exception");
            throw;
        }
    }
}
