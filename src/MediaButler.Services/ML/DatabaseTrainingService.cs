using MediaButler.Core.Common;
using MediaButler.Core.Enums;
using MediaButler.Data;
using MediaButler.ML.Configuration;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediaButler.Services.ML;

/// <summary>
/// Service for training ML models from database TrackedFiles data.
/// </summary>
public class DatabaseTrainingService : IDatabaseTrainingService
{
    private readonly ILogger<DatabaseTrainingService> _logger;
    private readonly MediaButlerDbContext _dbContext;
    private readonly IModelTrainingService _modelTrainingService;
    private readonly MLConfiguration _mlConfig;

    public DatabaseTrainingService(
        ILogger<DatabaseTrainingService> logger,
        MediaButlerDbContext dbContext,
        IModelTrainingService modelTrainingService,
        IOptions<MLConfiguration> mlConfig)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _modelTrainingService = modelTrainingService ?? throw new ArgumentNullException(nameof(modelTrainingService));
        _mlConfig = mlConfig?.Value ?? throw new ArgumentNullException(nameof(mlConfig));
    }

    /// <summary>
    /// Loads training samples from TrackedFiles table.
    /// </summary>
    public async Task<Result<List<TrainingSample>>> LoadTrainingSamplesFromDatabaseAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Loading training samples from TrackedFiles database");

            // Load all files with categories (excludes NULL or empty categories)
            var filesWithCategories = await _dbContext.TrackedFiles
                .Where(f => f.Category != null && f.Category != "" && f.Status==FileStatus.Moved)
                .Select(f => new { f.FileName, f.Category, f.CreatedDate })
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Found {Count} files moved with categories in database", filesWithCategories.Count);

            if (filesWithCategories.Count == 0)
            {
                return Result<List<TrainingSample>>.Failure("No training data found in database. Files must have Category values set.");
            }

            // Convert to training samples
            var trainingSamples = new List<TrainingSample>();
            foreach (var file in filesWithCategories)
            {
                trainingSamples.Add(new TrainingSample
                {
                    Filename = file.FileName,
                    Category = file.Category, // Use actual Italian category name from DB
                    Confidence = 1.0, // High confidence - these are user-confirmed categories
                    Source = TrainingSampleSource.UserFeedback,
                    CreatedAt = file.CreatedDate,
                    IsManuallyVerified = true
                });
            }

            // Show category distribution
            var categoryGroups = trainingSamples
                .GroupBy(s => s.Category)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToList();

            _logger.LogInformation("Top 10 categories:");
            foreach (var group in categoryGroups)
            {
                _logger.LogInformation("  {Category}: {Count} samples", group.Key, group.Count());
            }

            return Result<List<TrainingSample>>.Success(trainingSamples);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading training samples from database");
            return Result<List<TrainingSample>>.Failure($"Failed to load training data: {ex.Message}");
        }
    }

    /// <summary>
    /// Trains and saves a new ML model from database data.
    /// </summary>
    public async Task<Result<TrainedModelInfo>> TrainModelFromDatabaseAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting model training from database (session: {SessionId})", sessionId);

            // Load training samples
            var samplesResult = await LoadTrainingSamplesFromDatabaseAsync(cancellationToken);
            if (samplesResult.IsFailure)
            {
                return Result<TrainedModelInfo>.Failure(samplesResult.Error);
            }

            var trainingSamples = samplesResult.Value;
            _logger.LogInformation("Training model with {Count} samples from database", trainingSamples.Count);

            // Create training configuration with session ID
            var trainingConfig = new MediaButler.ML.Models.TrainingConfiguration
            {
                MaxEpochs = 100,
                LearningRate = 0.1,
                BatchSize = 32,
                ValidationSplit = 0.2,
                RandomSeed = 42,
                EarlyStoppingPatience = 10,
                MinimumImprovement = 0.001,
                EnableDataAugmentation = true,
                MaxTrainingTimeMinutes = 30,
                SessionId = sessionId
            };

            // Train the model
            var trainingResult = await _modelTrainingService.TrainModelAsync(
                trainingSamples,
                trainingConfig,
                cancellationToken);

            if (trainingResult.IsFailure)
            {
                _logger.LogError("Model training failed: {Error}", trainingResult.Error);
                return Result<TrainedModelInfo>.Failure(trainingResult.Error);
            }

            var trainedModel = trainingResult.Value;
            _logger.LogInformation($"Model v.{trainedModel.ModelVersion}  training completed with {trainedModel.ValidationMetrics.Accuracy:P2} accuracy");

            // Save the model
            var modelPath = Path.Combine(_mlConfig.ModelPath, "classification-model.zip");
            _logger.LogInformation("Saving model to: {ModelPath}", modelPath);

            var metadata = new ModelMetadata
            {
                ModelName = "TV Series Classifier (Database-trained)",
                Version = _mlConfig.ActiveModelVersion,
                CreatedAt = DateTime.UtcNow,
                Description = $"ML.NET model trained on {trainingSamples.Count} samples from TrackedFiles database",
                Author = "MediaButler ML Pipeline",
                Tags = new Dictionary<string, string>
                {
                    ["Environment"] = "Production",
                    ["TrainingSamples"] = trainingSamples.Count.ToString(),
                    ["Categories"] = trainingSamples.Select(s => s.Category).Distinct().Count().ToString(),
                    ["Accuracy"] = trainedModel.ValidationMetrics.Accuracy.ToString("P2"),
                    ["Language"] = "Italian",
                    ["Source"] = "Database"
                }
            };

            var saveResult = await _modelTrainingService.SaveModelAsync(
                trainedModel,
                modelPath,
                metadata);

            if (saveResult.IsFailure)
            {
                _logger.LogWarning("Model save returned failure but model may still be created: {Error}", saveResult.Error);
                // Check if file exists anyway
                if (!File.Exists(modelPath))
                {
                    return Result<TrainedModelInfo>.Failure($"Failed to save model: {saveResult.Error}");
                }
                _logger.LogInformation("Model file exists despite save error, continuing...");
            }

            _logger.LogInformation("Model training and save completed successfully");

            return Result<TrainedModelInfo>.Success(trainedModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during model training from database");
            return Result<TrainedModelInfo>.Failure($"Training failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Interface for database-based ML training.
/// </summary>
public interface IDatabaseTrainingService
{
    Task<Result<List<TrainingSample>>> LoadTrainingSamplesFromDatabaseAsync(CancellationToken cancellationToken = default);
    Task<Result<TrainedModelInfo>> TrainModelFromDatabaseAsync(string sessionId, CancellationToken cancellationToken = default);
}
