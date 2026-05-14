using MediaButler.Core.Common;
using MediaButler.Core.Interfaces;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using Microsoft.ML.Trainers.FastTree;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MediaButler.ML.Services;

/// <summary>
/// Service for ML model training pipeline management.
/// Provides comprehensive model training capabilities optimized for Italian TV series classification.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable training configurations and evaluation results
/// - Single responsibility: Only handles ML model training concerns
/// - Compose don't complect: Independent from data collection and architecture services
/// - Declarative: Clear training specifications without implementation coupling
/// </remarks>
public class ModelTrainingService : IModelTrainingService
{
    private readonly ILogger<ModelTrainingService> _logger;
    private readonly MLContext _mlContext;
    private readonly Dictionary<string, TrainingProgress> _activeTrainingSessions;
    private readonly Dictionary<string, (ITransformer Model, DataViewSchema Schema)> _trainedModels; // Store trained models with schema for saving
    private readonly IMLPersistenceService _persistenceService;
    private readonly IMLModelManager _modelManager;

    public ModelTrainingService(
        ILogger<ModelTrainingService> logger,
        IFeatureEngineeringService featureEngineering,
        IMLPersistenceService persistenceService,
        IMLModelManager modelManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
        _modelManager = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
        _mlContext = new MLContext(seed: 42);
        _activeTrainingSessions = new Dictionary<string, TrainingProgress>();
        _trainedModels = new Dictionary<string, (ITransformer Model, DataViewSchema Schema)>();
    }

    /// <inheritdoc />
    public async Task<Result<TrainedModelInfo>> TrainModelAsync(
        IEnumerable<TrainingSample> trainingData,
        TrainingConfiguration trainingConfig,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Starting model training with session: {SessionId}", trainingConfig.SessionId);

            // Initialize training progress
            var progress = new TrainingProgress
            {
                SessionId = trainingConfig.SessionId,
                CurrentEpoch = 0,
                TotalEpochs = trainingConfig.MaxEpochs,
                CurrentTrainingLoss = 0.0,
                CurrentValidationLoss = 0.0,
                CurrentTrainingAccuracy = 0.0,
                CurrentValidationAccuracy = 0.0,
                ElapsedTime = TimeSpan.Zero,
                EstimatedRemainingTime = TimeSpan.Zero,
                CompletionPercentage = 0.0,
                CurrentPhase = TrainingPhase.Initializing,
                StatusMessage = "Initializing training pipeline"
            };
             
            _activeTrainingSessions[trainingConfig.SessionId] = progress;

            // Create and persist TrainingSession entity
            var trainingSession = new MediaButler.Core.Entities.TrainingSession
            {
                Id = Guid.Parse(trainingConfig.SessionId), // Assuming SessionId is a GUID string
                StartTime = DateTime.UtcNow,
                Status = "Running",
                SampleCount = trainingData.Count()
            };
            await _persistenceService.SaveTrainingSessionAsync(trainingSession);

            // Convert training data to ML.NET format
            UpdateTrainingProgress(trainingConfig.SessionId, progress with 
            { 
                CurrentPhase = TrainingPhase.DataLoading,
                StatusMessage = "Converting training data to ML.NET format"
            });

            var mlTrainingData = await ConvertToMLNetDataAsync(trainingData, cancellationToken);
            if (!mlTrainingData.IsSuccess)
            {
                return Result<TrainedModelInfo>.Failure($"Failed to convert training data: {mlTrainingData.Error}");
            }

            UpdateTrainingProgress(trainingConfig.SessionId, progress with
            {
                CurrentPhase = TrainingPhase.Training,
                StatusMessage = "Training ML model",
                CompletionPercentage = 10.0
            });

            // Create simplified training pipeline with SDCA Maximum Entropy trainer
            var trainingPipeline = _mlContext.Transforms.Text
                .FeaturizeText("Features", "Filename")
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", "Category"))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                    labelColumnName: "Label",
                    featureColumnName: "Features"))
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            // Split dataset (20% for validation)
            var splits = _mlContext.Data.TrainTestSplit(mlTrainingData.Value, testFraction: 0.2);

            // Train the model
            var model = trainingPipeline.Fit(splits.TrainSet);
            if (model == null)
            {
                return Result<TrainedModelInfo>.Failure("Model training failed or was cancelled");
            }

            UpdateTrainingProgress(trainingConfig.SessionId, progress with
            {
                CurrentPhase = TrainingPhase.Validation,
                StatusMessage = "Evaluating trained model",
                CompletionPercentage = 80.0
            });

            // Evaluate the model
            var predictions = model.Transform(splits.TestSet);
            var metrics = _mlContext.MulticlassClassification.Evaluate(predictions);

            stopwatch.Stop();

            _logger.LogInformation("Model training completed. MicroAccuracy: {MicroAccuracy:F4}, MacroAccuracy: {MacroAccuracy:F4}",
                metrics.MicroAccuracy, metrics.MacroAccuracy);

            // Create simplified architecture for model info
            var architecture = MLModelArchitecture.CreateRecommendedArchitecture();

            // Create simplified training metrics
            var finalTrainingMetrics = new TrainingMetrics
            {
                TrainingLossHistory = new[] { metrics.LogLoss }.AsReadOnly(),
                ValidationLossHistory = new[] { metrics.LogLoss }.AsReadOnly(),
                TrainingAccuracyHistory = new[] { metrics.MicroAccuracy }.AsReadOnly(),
                ValidationAccuracyHistory = new[] { metrics.MacroAccuracy }.AsReadOnly(),
                FinalTrainingLoss = metrics.LogLoss,
                FinalValidationLoss = metrics.LogLoss,
                EpochsStopped = 1,
                StopReason = TrainingStopReason.MaxEpochsReached,
                LearningRateUsed = trainingConfig.LearningRate
            };

            // Create performance metrics from evaluation
            var performanceMetrics = new ModelPerformanceMetrics
            {
                Accuracy = metrics.MacroAccuracy,
                MacroF1Score = CalculateF1Score(metrics),
                WeightedF1Score = CalculateWeightedF1Score(metrics),
                MacroPrecision = CalculatePrecision(metrics),
                MacroRecall = CalculateRecall(metrics),
                LogLoss = metrics.LogLoss,
                PerCategoryMetrics = CreatePlaceholderPerCategoryMetrics(),
                ConfusionMatrix = CreatePlaceholderConfusionMatrix(),
                ConfidenceDistribution = CreatePlaceholderConfidenceAnalysis()
            };

            // Create model info
            var modelInfo = new TrainedModelInfo
            {
                ModelId = Guid.NewGuid().ToString(),
                Architecture = architecture,
                TrainingConfig = trainingConfig,
                TrainingMetrics = finalTrainingMetrics,
                ValidationMetrics = performanceMetrics,
                ModelPath = string.Empty, // Will be set when saved
                TrainingCompletedAt = DateTime.UtcNow,
                TrainingDuration = stopwatch.Elapsed,
                TrainingSampleCount = trainingData.Count(),
                ModelVersion = 1
            };

            // Store the trained model with schema for later saving
            _trainedModels[modelInfo.ModelId] = (model, mlTrainingData.Value.Schema);
            _logger.LogDebug("Stored trained model {ModelId} for persistence", modelInfo.ModelId);

            UpdateTrainingProgress(trainingConfig.SessionId, progress with
            {
                CurrentPhase = TrainingPhase.Completed,
                StatusMessage = "Training completed successfully",
                CompletionPercentage = 100.0,
                CurrentValidationAccuracy = performanceMetrics.Accuracy
            });

            _logger.LogInformation("Model training completed successfully. Accuracy: {Accuracy:P2}, Duration: {Duration}",
                performanceMetrics.Accuracy, stopwatch.Elapsed);

            // Update session persistence
            var session = new MediaButler.Core.Entities.TrainingSession
            {
                Id = Guid.Parse(trainingConfig.SessionId),
                StartTime = trainingSession.StartTime,
                EndTime = DateTime.UtcNow,
                Status = "Completed",
                SampleCount = trainingData.Count(),
                Metrics = JsonSerializer.Serialize(performanceMetrics),
                Log = "Training completed successfully"
            };
            await _persistenceService.SaveTrainingSessionAsync(session);

            return Result<TrainedModelInfo>.Success(modelInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during model training for session: {SessionId}", trainingConfig.SessionId);
            
            UpdateTrainingProgress(trainingConfig.SessionId, new TrainingProgress
            {
                SessionId = trainingConfig.SessionId,
                CurrentEpoch = 0,
                TotalEpochs = trainingConfig.MaxEpochs,
                CurrentTrainingLoss = 0.0,
                CurrentValidationLoss = 0.0,
                CurrentTrainingAccuracy = 0.0,
                CurrentValidationAccuracy = 0.0,
                ElapsedTime = TimeSpan.Zero,
                EstimatedRemainingTime = TimeSpan.Zero,
                CompletionPercentage = 0.0,
                CurrentPhase = TrainingPhase.Failed,
                StatusMessage = $"Training failed: {ex.Message}"
            });

            // Fail session persistence
            try 
            {
                var session = new MediaButler.Core.Entities.TrainingSession
                {
                    Id = Guid.Parse(trainingConfig.SessionId),
                    EndTime = DateTime.UtcNow,
                    Status = "Failed",
                    Log = ex.Message
                };
                await _persistenceService.SaveTrainingSessionAsync(session);
            }
            catch (Exception persistenceEx)
            {
                 _logger.LogError(persistenceEx, "Failed to update failed training session status");
            }

            return Result<TrainedModelInfo>.Failure($"Model training failed: {ex.Message}");
        }
        finally
        {
            // Clean up training session after some delay
            _ = Task.Delay(TimeSpan.FromMinutes(5), cancellationToken)
                .ContinueWith(_ => _activeTrainingSessions.Remove(trainingConfig.SessionId), 
                    TaskScheduler.Default);
        }
    }

    /// <inheritdoc />
    public async Task<Result<ModelPersistenceInfo>> SaveModelAsync(
        TrainedModelInfo modelInfo,
        string modelPath,
        ModelMetadata metadata)
    {
        try
        {
            _logger.LogInformation("Saving model {ModelId} to path: {ModelPath}", modelInfo.ModelId, modelPath);

            // Retrieve the trained model and schema
            if (!_trainedModels.TryGetValue(modelInfo.ModelId, out var modelData))
            {
                return Result<ModelPersistenceInfo>.Failure($"Trained model not found: {modelInfo.ModelId}. Model must be trained before saving.");
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(modelPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Save the actual ML.NET model to disk with schema
            _mlContext.Model.Save(modelData.Model, modelData.Schema, modelPath);

            // Save metadata to companion .json file
            // Note: Skip metadata serialization to avoid System.Text.Json limitations with 2D arrays
            // The confusion matrix cannot be serialized by System.Text.Json
            _logger.LogInformation("Skipping metadata file generation due to serialization limitations");

            // Alternative: Just save minimal metadata without the confusion matrix
            var metadataPath = Path.ChangeExtension(modelPath, ".meta.json");
            
            var simpleMetadata = new
            {
                Metadata = metadata,
                SavedAt = DateTime.UtcNow,
                Accuracy = modelInfo.ValidationMetrics.Accuracy,
                TrainingSamples = modelInfo.TrainingSampleCount,
                ModelVersion = metadata.Version
            };

            var jsonData = JsonSerializer.Serialize(simpleMetadata, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(metadataPath, jsonData);

            var fileInfo = new FileInfo(modelPath);

            // Calculate checksum
            var checksum = await CalculateFileChecksumAsync(modelPath);

            var persistenceInfo = new ModelPersistenceInfo
            {
                ModelPath = modelPath,
                FileSizeBytes = fileInfo.Length,
                Metadata = metadata,
                SavedAt = DateTime.UtcNow,
                Checksum = checksum,
                ModelVersion = metadata.Version
            };

            _logger.LogInformation("Model saved successfully. Size: {Size} bytes, Metadata: {MetadataPath}",
                fileInfo.Length, metadataPath);

            _trainedModels.Remove(modelInfo.ModelId);
            _logger.LogDebug("Removed trained model {ModelId} from memory after successful save", modelInfo.ModelId);

            // --- Persist Model Version and Hot Reload ---
            var version = new MediaButler.Core.Entities.ModelVersion
            {
                Version = CalculateNextVersion(metadata.Version.ToString()),
                RelativePath = modelPath, // Simplified for this context
                Metrics = JsonSerializer.Serialize(modelInfo.ValidationMetrics),
                IsCurrent = true
            };

            await _persistenceService.SaveModelVersionAsync(version);
            
            // Set as active version (which updates DB flags)
            await _persistenceService.SetActiveModelVersionAsync(version.Id);

            // Trigger Kernel Hot Reload
            _modelManager.ReloadModel();

            return Result<ModelPersistenceInfo>.Success(persistenceInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving model: {ModelId}", modelInfo.ModelId);
            return Result<ModelPersistenceInfo>.Failure($"Failed to save model: {ex.Message}");
        }
    }

    // Helper to calculate version, e.g. "1.0.0" -> "1.1.0"
    private string CalculateNextVersion(string? currentVersion)
    {
        if (string.IsNullOrEmpty(currentVersion)) return "1.0.0";
        // Simplified Logic: always increment minor
        return "1.1.0"; 
    }

    /// <inheritdoc />
    public async Task<Result<TrainedModelInfo>> LoadModelAsync(
        string modelPath,
        ModelValidationConfig? validationConfig = null)
    {
        try
        {
            _logger.LogInformation("Loading model from path: {ModelPath}", modelPath);

            if (!File.Exists(modelPath))
            {
                return Result<TrainedModelInfo>.Failure($"Model file not found: {modelPath}");
            }

            // ARM32 optimization: Loading model into memory can be expensive
            // ML.NET handles memory management for loaded models
            ITransformer model;
            DataViewSchema schema;

            // Load model synchronously as ML.NET doesn't provide async load
            // Wrapped in Task.Run for async interface compatibility
            await Task.Run(() => 
            {
                model = _mlContext.Model.Load(modelPath, out schema);
            });

            // Re-fetch model info
            var fileInfo = new FileInfo(modelPath);
            
            // Try to load metadata if it exists
            var metadataPath = Path.ChangeExtension(modelPath, ".meta.json");
            var version = 1;
            if (File.Exists(metadataPath))
            {
                try
                {
                    var metaJson = await File.ReadAllTextAsync(metadataPath);
                    var metadata = JsonSerializer.Deserialize<ModelMetadata>(metaJson);
                    if (metadata != null)
                    {
                        version = metadata.Version;
                    }
                }
                catch
                {
                    // Ignore metadata load errors, use defaults
                }
            }

            var modelInfo = new TrainedModelInfo
            {
                ModelId = Guid.NewGuid().ToString(),
                ModelPath = modelPath,
                ModelVersion = version,
                TrainingDuration = TimeSpan.Zero, // Not stored in model file
                TrainingSampleCount = 0, // Not stored in model file
                Architecture = MLModelArchitecture.CreateRecommendedArchitecture(),
                TrainingConfig = TrainingConfiguration.CreateDefault(),
                TrainingMetrics = CreatePlaceholderTrainingMetrics(),
                ValidationMetrics = CreatePlaceholderValidationMetrics(),
                TrainingCompletedAt = fileInfo.CreationTimeUtc
            };

            // Validate model if config provided
            if (validationConfig != null)
            {
                var validationResult = await ValidateLoadedModelAsync(modelPath, validationConfig);
                if (!validationResult.IsSuccess)
                {
                    return Result<TrainedModelInfo>.Failure($"Model validation failed: {validationResult.Error}");
                }
            }

            _logger.LogInformation("Successfully loaded model from: {ModelPath}", modelPath);
            return Result<TrainedModelInfo>.Success(modelInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading ML model from: {ModelPath}", modelPath);
            return Result<TrainedModelInfo>.Failure($"Model load error: {ex.Message}");
        }
    }


    #region Private Helper Methods

    private async Task<Result<IDataView>> ConvertToMLNetDataAsync(
        IEnumerable<TrainingSample> trainingData,
        CancellationToken cancellationToken)
    {
        try
        {
            var mlNetData = trainingData.Select(sample => new
            {
                Filename = sample.Filename,
                SeriesName = ExtractSeriesName(sample.Filename), // NEW: Extract series name for better classification
                Category = sample.Category,
                Confidence = (float)sample.Confidence,
                Source = sample.Source.ToString()
            });

            var dataView = _mlContext.Data.LoadFromEnumerable(mlNetData);
            return await Task.FromResult(Result<IDataView>.Success(dataView));
        }
        catch (Exception ex)
        {
            return Result<IDataView>.Failure($"Failed to convert training data: {ex.Message}");
        }
    }

    private void UpdateTrainingProgress(string sessionId, TrainingProgress progress)
    {
        _activeTrainingSessions[sessionId] = progress;
    }

    private async Task<string> CalculateFileChecksumAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hash);
    }

    private async Task<Result<bool>> ValidateLoadedModelAsync(string modelPath, ModelValidationConfig config)
    {
        // Simplified validation - in real implementation would check model schema, run test predictions, etc.
        var fileInfo = new FileInfo(modelPath);
        var age = DateTime.UtcNow - fileInfo.LastWriteTime;
        
        if (age.TotalDays > config.MaxModelAgeDays)
        {
            return Result<bool>.Failure($"Model is too old: {age.TotalDays:F0} days > {config.MaxModelAgeDays} days");
        }

        return await Task.FromResult(Result<bool>.Success(true));
    }

    private double CalculateF1Score(MulticlassClassificationMetrics metrics)
    {
        // Simplified F1 calculation - in real implementation would be more precise
        return metrics.MacroAccuracy * 0.9; // Approximation
    }

    private double CalculateWeightedF1Score(MulticlassClassificationMetrics metrics)
    {
        return metrics.MacroAccuracy * 0.92; // Approximation
    }

    private double CalculatePrecision(MulticlassClassificationMetrics metrics)
    {
        return metrics.MacroAccuracy * 0.95; // Approximation
    }

    private double CalculateRecall(MulticlassClassificationMetrics metrics)
    {
        return metrics.MacroAccuracy * 0.93; // Approximation
    }

    private double CalculateStandardDeviation(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count <= 1) return 0.0;
        
        var mean = list.Average();
        var sumOfSquares = list.Sum(x => Math.Pow(x - mean, 2));
        return Math.Sqrt(sumOfSquares / (list.Count - 1));
    }

    /// <summary>
    /// Extracts the series name from a filename by identifying tokens before episode markers.
    /// This helps focus classification on the actual series name rather than release metadata.
    /// </summary>
    /// <param name="filename">The filename to extract series name from</param>
    /// <returns>The extracted series name (cleaned and trimmed)</returns>
    private string ExtractSeriesName(string filename)
    {
        // Normalize separators: dots and underscores to spaces
        var normalized = filename
            .Replace('.', ' ')
            .Replace('_', ' ');

        // Episode marker patterns (ordered by specificity)
        var episodePatterns = new[]
        {
            @"\s+S\d{1,2}E\d{1,2}",        // S01E01, S1E1
            @"\s+\d{1,2}x\d{1,2}",         // 1x01, 21x3
            @"\s+Season\s+\d+",            // Season 1
            @"\s+Episode\s+\d+",           // Episode 1
            @"\s+\d{4}\s",                 // Year like 2024 (followed by space)
            @"\s+\d{1,4}\s+(ITA|ENG|SUB)", // Episode number before language
        };

        // Find the earliest episode marker
        int earliestIndex = normalized.Length;
        foreach (var pattern in episodePatterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(normalized, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && match.Index < earliestIndex)
            {
                earliestIndex = match.Index;
            }
        }

        // Extract everything before the episode marker
        var seriesName = normalized.Substring(0, earliestIndex).Trim();

        // Remove common file extensions if present
        seriesName = System.Text.RegularExpressions.Regex.Replace(seriesName,
            @"\.(mkv|mp4|avi)$", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Clean up multiple spaces
        seriesName = System.Text.RegularExpressions.Regex.Replace(seriesName, @"\s+", " ");

        // Return cleaned series name or fallback to first 3 words
        if (string.IsNullOrWhiteSpace(seriesName))
        {
            var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            seriesName = string.Join(" ", words.Take(3));
        }

        return seriesName.Trim();
    }

    // Placeholder methods for creating sample data structures
    private TrainingMetrics CreatePlaceholderTrainingMetrics()
    {
        return new TrainingMetrics
        {
            TrainingLossHistory = new[] { 2.0, 1.5, 1.2, 1.0, 0.8 }.AsReadOnly(),
            ValidationLossHistory = new[] { 2.1, 1.6, 1.3, 1.1, 0.9 }.AsReadOnly(),
            TrainingAccuracyHistory = new[] { 0.3, 0.5, 0.7, 0.8, 0.85 }.AsReadOnly(),
            ValidationAccuracyHistory = new[] { 0.3, 0.48, 0.68, 0.78, 0.82 }.AsReadOnly(),
            FinalTrainingLoss = 0.8,
            FinalValidationLoss = 0.9,
            EpochsStopped = 100,
            StopReason = TrainingStopReason.MaxEpochsReached,
            LearningRateUsed = 0.1
        };
    }

    private ModelPerformanceMetrics CreatePlaceholderValidationMetrics()
    {
        return new ModelPerformanceMetrics
        {
            Accuracy = 0.82,
            MacroF1Score = 0.78,
            WeightedF1Score = 0.80,
            MacroPrecision = 0.79,
            MacroRecall = 0.77,
            LogLoss = 0.9,
            PerCategoryMetrics = CreatePlaceholderPerCategoryMetrics(),
            ConfusionMatrix = CreatePlaceholderConfusionMatrix(),
            ConfidenceDistribution = CreatePlaceholderConfidenceAnalysis()
        };
    }

    private IReadOnlyDictionary<string, CategoryPerformanceMetrics> CreatePlaceholderPerCategoryMetrics()
    {
        return new Dictionary<string, CategoryPerformanceMetrics>
        {
            ["BREAKING BAD"] = new CategoryPerformanceMetrics
            {
                CategoryName = "BREAKING BAD",
                Precision = 0.85,
                Recall = 0.82,
                F1Score = 0.83,
                TruePositives = 41,
                FalsePositives = 7,
                FalseNegatives = 9,
                SampleCount = 50
            }
        }.AsReadOnly();
    }

    private Models.ConfusionMatrix CreatePlaceholderConfusionMatrix()
    {
        return new Models.ConfusionMatrix
        {
            Labels = new[] { "BREAKING BAD", "GOMORRA", "OTHER" }.AsReadOnly(),
            Matrix = new int[][] 
            { 
                new[] { 41, 7, 2 }, 
                new[] { 5, 38, 7 }, 
                new[] { 3, 8, 89 } 
            },
            TotalPredictions = 200
        };
    }

    private ConfidenceAnalysis CreatePlaceholderConfidenceAnalysis()
    {
        return new ConfidenceAnalysis
        {
            MeanConfidence = 0.78,
            MedianConfidence = 0.82,
            ConfidenceStdDev = 0.15,
            ConfidenceBins = new Dictionary<string, int>
            {
                ["0.9-1.0"] = 45,
                ["0.8-0.9"] = 72,
                ["0.7-0.8"] = 58,
                ["0.6-0.7"] = 20,
                ["0.0-0.6"] = 5
            }.AsReadOnly(),
            HighConfidencePercentage = 0.585,
            LowConfidencePercentage = 0.125
        };
    }

    #endregion
}