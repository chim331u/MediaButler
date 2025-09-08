using MediaButler.Core.Common;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;
using Microsoft.Extensions.Logging;

namespace MediaButler.ML.Services;

/// <summary>
/// Service for ML model architecture management and configuration.
/// Provides comprehensive model lifecycle management optimized for Italian TV series classification.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable model configurations and immutable evaluation results
/// - Single responsibility: Only handles ML model architecture concerns
/// - Compose don't complect: Independent from training and prediction services
/// - Declarative: Clear model specifications without implementation coupling
/// </remarks>
public class MLModelService : IMLModelService
{
    private readonly ILogger<MLModelService> _logger;

    public MLModelService(ILogger<MLModelService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result<MLModelArchitecture>> CreateClassificationModelAsync(ModelConfiguration modelConfig)
    {
        try
        {
            _logger.LogInformation("Creating classification model architecture for type: {ModelType}, Algorithm: {Algorithm}",
                modelConfig.ModelType, modelConfig.AlgorithmType);

            if (modelConfig.ModelType != ModelType.MultiClassClassification)
            {
                return Result<MLModelArchitecture>.Failure("Only multi-class classification is supported for TV series categorization");
            }

            var algorithm = CreateAlgorithmConfiguration(modelConfig.AlgorithmType, modelConfig.CustomHyperparameters);
            var featurePipeline = FeaturePipelineConfig.CreateComprehensive();
            var evaluationMetrics = ModelEvaluationMetrics.CreateForClassification();
            var crossValidation = CrossValidationConfig.CreateStratified(folds: 5);
            var italianOptimization = ItalianContentConfig.CreateDefault();
            var performance = PerformanceRequirements.CreateForARM32();

            var architecture = new MLModelArchitecture
            {
                ArchitectureId = $"italian-tv-{modelConfig.AlgorithmType.ToString().ToLower()}-{DateTime.UtcNow:yyyyMMdd}",
                Version = "1.0.0",
                ModelType = modelConfig.ModelType,
                Algorithm = algorithm,
                FeaturePipeline = featurePipeline,
                EvaluationMetrics = evaluationMetrics,
                CrossValidation = crossValidation,
                ItalianOptimization = italianOptimization,
                Performance = performance,
                CreatedAt = DateTime.UtcNow,
                Metadata = $"Created for {modelConfig.AlgorithmType} algorithm with Italian content optimization"
            };

            // Add resource estimate
            var resourceEstimate = await EstimateResourceRequirementsAsync(architecture);
            if (resourceEstimate.IsSuccess)
            {
                architecture = architecture with { ResourceEstimate = resourceEstimate.Value };
            }

            _logger.LogInformation("Successfully created model architecture: {ArchitectureId}", architecture.ArchitectureId);
            
            return Result<MLModelArchitecture>.Success(architecture);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating classification model architecture");
            return Result<MLModelArchitecture>.Failure($"Failed to create model architecture: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<FeaturePipelineConfig>> ConfigureFeaturePipelineAsync(FeaturePipelineConfiguration featureConfig)
    {
        try
        {
            _logger.LogInformation("Configuring feature pipeline with {MaxFeatures} max features", featureConfig.MaxFeatures);

            var normalization = new NormalizationConfig
            {
                NormalizeNumerical = featureConfig.NormalizeFeatures,
                Method = NormalizationMethod.MinMax
            };

            var categoricalEncoding = CategoricalEncodingConfig.CreateOptimal();
            
            var featureSelection = new FeatureSelectionConfig
            {
                MaxFeatures = featureConfig.MaxFeatures,
                ImportanceThreshold = featureConfig.FeatureImportanceThreshold
            };

            var textFeatures = TextFeatureConfig.CreateForFilenames();

            var pipelineConfig = new FeaturePipelineConfig
            {
                Normalization = normalization,
                CategoricalEncoding = categoricalEncoding,
                FeatureSelection = featureSelection,
                TextFeatures = textFeatures
            };

            _logger.LogInformation("Feature pipeline configured successfully");
            
            return await Task.FromResult(Result<FeaturePipelineConfig>.Success(pipelineConfig));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring feature pipeline");
            return Result<FeaturePipelineConfig>.Failure($"Failed to configure feature pipeline: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<CrossValidationConfig>> DefineCrossValidationStrategyAsync(ValidationConfiguration validationConfig)
    {
        try
        {
            _logger.LogInformation("Defining cross-validation strategy with {Folds} folds, Stratified: {UseStratification}",
                validationConfig.Folds, validationConfig.UseStratification);

            if (validationConfig.Folds < 2 || validationConfig.Folds > 10)
            {
                return Result<CrossValidationConfig>.Failure("Cross-validation folds must be between 2 and 10");
            }

            var crossValidationConfig = new CrossValidationConfig
            {
                Folds = validationConfig.Folds,
                UseStratification = validationConfig.UseStratification,
                RandomSeed = validationConfig.RandomSeed,
                SamplingStrategy = validationConfig.UseStratification 
                    ? ValidationSamplingStrategy.Stratified 
                    : ValidationSamplingStrategy.Random
            };

            _logger.LogInformation("Cross-validation strategy defined successfully");
            
            return await Task.FromResult(Result<CrossValidationConfig>.Success(crossValidationConfig));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error defining cross-validation strategy");
            return Result<CrossValidationConfig>.Failure($"Failed to define validation strategy: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<ModelEvaluationMetrics>> DefineEvaluationMetricsAsync(EvaluationMetricsConfiguration metricsConfig)
    {
        try
        {
            _logger.LogInformation("Defining evaluation metrics with target accuracy: {TargetAccuracy}", metricsConfig.TargetAccuracy);

            if (metricsConfig.TargetAccuracy < 0.0 || metricsConfig.TargetAccuracy > 1.0)
            {
                return Result<ModelEvaluationMetrics>.Failure("Target accuracy must be between 0.0 and 1.0");
            }

            var evaluationMetrics = new ModelEvaluationMetrics
            {
                PrimaryMetrics = new[]
                {
                    EvaluationMetricType.Accuracy,
                    EvaluationMetricType.MacroF1Score,
                    EvaluationMetricType.WeightedF1Score
                }.AsReadOnly(),
                SecondaryMetrics = new[]
                {
                    EvaluationMetricType.Precision,
                    EvaluationMetricType.Recall,
                    EvaluationMetricType.LogLoss
                }.AsReadOnly(),
                TargetAccuracy = metricsConfig.TargetAccuracy,
                MinimumF1Score = Math.Max(0.70, metricsConfig.TargetAccuracy - 0.1), // F1 should be close to accuracy
                GenerateConfusionMatrix = metricsConfig.GenerateConfusionMatrix,
                CalculatePerCategoryMetrics = metricsConfig.CalculatePerCategoryMetrics
            };

            _logger.LogInformation("Evaluation metrics defined successfully");
            
            return await Task.FromResult(Result<ModelEvaluationMetrics>.Success(evaluationMetrics));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error defining evaluation metrics");
            return Result<ModelEvaluationMetrics>.Failure($"Failed to define evaluation metrics: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<ArchitectureValidationResult>> ValidateModelArchitectureAsync(MLModelArchitecture architecture)
    {
        try
        {
            _logger.LogInformation("Validating model architecture: {ArchitectureId}", architecture.ArchitectureId);

            var issues = new List<ValidationIssue>();
            var recommendations = new List<string>();

            // Validate basic architecture completeness
            if (!architecture.IsValid)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Critical,
                    Description = "Architecture configuration is incomplete",
                    AffectedItems = new[] { architecture.ArchitectureId }.AsReadOnly()
                });
            }

            // Validate performance requirements for ARM32
            if (architecture.ResourceEstimate != null && !architecture.ResourceEstimate.MeetsRequirements(architecture.Performance))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Warning,
                    Description = "Resource estimates may exceed ARM32 performance requirements",
                    AffectedItems = new[] { "ResourceEstimate" }.AsReadOnly()
                });
                recommendations.Add("Optimize hyperparameters for lower resource usage");
            }

            // Validate algorithm selection for Italian content
            if (architecture.Algorithm.AlgorithmType == AlgorithmType.SVM && 
                architecture.ItalianOptimization.ReleaseGroupPatterns.Count > 20)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Info,
                    Description = "SVM may not be optimal for high-cardinality Italian release group patterns",
                    AffectedItems = new[] { "Algorithm.AlgorithmType" }.AsReadOnly()
                });
                recommendations.Add("LightGBM typically performs better with Italian content patterns");
            }

            // Validate cross-validation configuration
            if (!architecture.CrossValidation.UseStratification && architecture.ItalianOptimization.ReleaseGroupPatterns.Count > 10)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Warning,
                    Description = "Non-stratified cross-validation may not work well with imbalanced Italian categories",
                    AffectedItems = new[] { "CrossValidation.UseStratification" }.AsReadOnly()
                });
            }

            var status = issues.Any(i => i.Severity == IssueSeverity.Critical) 
                ? ArchitectureValidationStatus.Critical 
                : issues.Any(i => i.Severity == IssueSeverity.Error) 
                    ? ArchitectureValidationStatus.Error
                    : issues.Any() 
                        ? ArchitectureValidationStatus.Warning 
                        : ArchitectureValidationStatus.Valid;

            var validationResult = new ArchitectureValidationResult
            {
                Status = status,
                Issues = issues.AsReadOnly(),
                Recommendations = recommendations.AsReadOnly()
            };

            _logger.LogInformation("Architecture validation completed. Status: {Status}, Issues: {IssueCount}", 
                status, issues.Count);
            
            return await Task.FromResult(Result<ArchitectureValidationResult>.Success(validationResult));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating model architecture");
            return Result<ArchitectureValidationResult>.Failure($"Failed to validate architecture: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<ModelResourceEstimate>> EstimateResourceRequirementsAsync(MLModelArchitecture architecture)
    {
        try
        {
            _logger.LogInformation("Estimating resource requirements for architecture: {ArchitectureId}", architecture.ArchitectureId);

            // Base estimates for different algorithms (empirically determined)
            var baseEstimates = GetBaseResourceEstimates(architecture.Algorithm.AlgorithmType);
            
            // Adjust for feature count
            var featureMultiplier = Math.Max(1.0, architecture.FeaturePipeline.FeatureSelection.MaxFeatures / 50.0);
            
            // Adjust for Italian optimization complexity
            var italianMultiplier = 1.0 + (architecture.ItalianOptimization.ReleaseGroupPatterns.Count * 0.02);
            
            // Adjust for cross-validation folds
            var trainingTimeMultiplier = architecture.CrossValidation.Folds;

            var estimate = new ModelResourceEstimate
            {
                EstimatedMemoryMB = baseEstimates.Memory * featureMultiplier * italianMultiplier,
                EstimatedLatencyMs = baseEstimates.Latency * featureMultiplier,
                EstimatedModelSizeMB = baseEstimates.ModelSize * featureMultiplier,
                EstimatedTrainingTimeMinutes = baseEstimates.TrainingTime * trainingTimeMultiplier * italianMultiplier
            };

            _logger.LogInformation("Resource requirements estimated: {Memory}MB memory, {Latency}ms latency, {Size}MB model size",
                estimate.EstimatedMemoryMB, estimate.EstimatedLatencyMs, estimate.EstimatedModelSizeMB);
            
            return await Task.FromResult(Result<ModelResourceEstimate>.Success(estimate));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error estimating resource requirements");
            return Result<ModelResourceEstimate>.Failure($"Failed to estimate resources: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<MLModelArchitecture>> GetRecommendedArchitectureAsync()
    {
        try
        {
            _logger.LogInformation("Creating recommended architecture for Italian TV series classification");

            var architecture = MLModelArchitecture.CreateRecommendedArchitecture();
            
            // Add resource estimate to recommended architecture
            var resourceEstimate = await EstimateResourceRequirementsAsync(architecture);
            if (resourceEstimate.IsSuccess)
            {
                architecture = architecture with { ResourceEstimate = resourceEstimate.Value };
            }

            _logger.LogInformation("Recommended architecture created: {ArchitectureId}", architecture.ArchitectureId);
            
            return Result<MLModelArchitecture>.Success(architecture);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating recommended architecture");
            return Result<MLModelArchitecture>.Failure($"Failed to create recommended architecture: {ex.Message}");
        }
    }

    private static AlgorithmConfiguration CreateAlgorithmConfiguration(AlgorithmType algorithmType, Dictionary<string, object>? customHyperparameters)
    {
        var baseConfig = algorithmType switch
        {
            AlgorithmType.LightGBM => AlgorithmConfiguration.CreateOptimizedForItalianContent(),
            AlgorithmType.FastTree => CreateFastTreeConfig(),
            AlgorithmType.LogisticRegression => CreateLogisticRegressionConfig(),
            AlgorithmType.SVM => CreateSVMConfig(),
            AlgorithmType.RandomForest => CreateRandomForestConfig(),
            _ => throw new ArgumentException($"Unsupported algorithm type: {algorithmType}")
        };

        // Merge custom hyperparameters if provided
        if (customHyperparameters != null)
        {
            var mergedHyperparameters = new Dictionary<string, object>(baseConfig.Hyperparameters);
            foreach (var kvp in customHyperparameters)
            {
                mergedHyperparameters[kvp.Key] = kvp.Value;
            }
            
            return baseConfig with { Hyperparameters = mergedHyperparameters };
        }

        return baseConfig;
    }

    private static AlgorithmConfiguration CreateFastTreeConfig() => new()
    {
        AlgorithmType = AlgorithmType.FastTree,
        MaxIterations = 300,
        LearningRate = 0.15,
        RegularizationStrength = 0.05,
        FeatureImportanceThreshold = 0.001,
        Hyperparameters = new Dictionary<string, object>
        {
            ["num_trees"] = 300,
            ["num_leaves"] = 31,
            ["learning_rate"] = 0.15,
            ["min_data_per_leaf"] = 10
        }
    };

    private static AlgorithmConfiguration CreateLogisticRegressionConfig() => new()
    {
        AlgorithmType = AlgorithmType.LogisticRegression,
        MaxIterations = 1000,
        LearningRate = 0.01,
        RegularizationStrength = 0.1,
        FeatureImportanceThreshold = 0.01,
        Hyperparameters = new Dictionary<string, object>
        {
            ["l1_regularization"] = 0.05,
            ["l2_regularization"] = 0.05,
            ["optimize_tolerance"] = 1e-7
        }
    };

    private static AlgorithmConfiguration CreateSVMConfig() => new()
    {
        AlgorithmType = AlgorithmType.SVM,
        MaxIterations = 500,
        LearningRate = 0.01,
        RegularizationStrength = 1.0,
        FeatureImportanceThreshold = 0.001,
        Hyperparameters = new Dictionary<string, object>
        {
            ["kernel"] = "rbf",
            ["C"] = 1.0,
            ["gamma"] = "scale"
        }
    };

    private static AlgorithmConfiguration CreateRandomForestConfig() => new()
    {
        AlgorithmType = AlgorithmType.RandomForest,
        MaxIterations = 100,
        LearningRate = 0.1,
        RegularizationStrength = 0.0,
        FeatureImportanceThreshold = 0.001,
        Hyperparameters = new Dictionary<string, object>
        {
            ["n_estimators"] = 100,
            ["max_depth"] = 10,
            ["min_samples_split"] = 5,
            ["min_samples_leaf"] = 2
        }
    };

    private static (double Memory, double Latency, double ModelSize, double TrainingTime) GetBaseResourceEstimates(AlgorithmType algorithmType)
    {
        return algorithmType switch
        {
            AlgorithmType.LightGBM => (80.0, 50.0, 15.0, 5.0),
            AlgorithmType.FastTree => (60.0, 30.0, 10.0, 3.0),
            AlgorithmType.LogisticRegression => (40.0, 10.0, 5.0, 2.0),
            AlgorithmType.SVM => (120.0, 100.0, 20.0, 10.0),
            AlgorithmType.RandomForest => (100.0, 80.0, 25.0, 8.0),
            _ => (60.0, 50.0, 15.0, 5.0) // Default estimates
        };
    }
}