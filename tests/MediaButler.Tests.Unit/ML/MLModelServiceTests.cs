using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MediaButler.ML.Services;
using MediaButler.ML.Models;
using Xunit;

namespace MediaButler.Tests.Unit.ML;

/// <summary>
/// Unit tests for MLModelService covering model architecture management.
/// Tests focus on Italian content optimization and ARM32 deployment requirements.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" testing principles:
/// - Test behavior, not implementation
/// - Clear Given-When-Then structure  
/// - Values over state testing
/// - Independent, deterministic tests
/// </remarks>
public class MLModelServiceTests
{
    private readonly Mock<ILogger<MLModelService>> _mockLogger;
    private readonly MLModelService _service;

    public MLModelServiceTests()
    {
        _mockLogger = new Mock<ILogger<MLModelService>>();
        _service = new MLModelService(_mockLogger.Object);
    }

    [Fact]
    public async Task CreateClassificationModelAsync_WithValidConfiguration_ReturnsSuccess()
    {
        // Given - Valid multi-class classification configuration
        var modelConfig = new ModelConfiguration
        {
            ModelType = ModelType.MultiClassClassification,
            AlgorithmType = AlgorithmType.LightGBM
        };

        // When - Creating classification model
        var result = await _service.CreateClassificationModelAsync(modelConfig);

        // Then - Should succeed and return valid architecture
        result.IsSuccess.Should().BeTrue();
        
        var architecture = result.Value;
        architecture.Should().NotBeNull();
        architecture.ModelType.Should().Be(ModelType.MultiClassClassification);
        architecture.Algorithm.AlgorithmType.Should().Be(AlgorithmType.LightGBM);
        architecture.IsValid.Should().BeTrue();
        
        // Should include Italian optimization
        architecture.ItalianOptimization.Should().NotBeNull();
        architecture.ItalianOptimization.ReleaseGroupPatterns.Should().NotBeEmpty();
        architecture.ItalianOptimization.ReleaseGroupPatterns.Should().Contain("NovaRip");
        architecture.ItalianOptimization.ReleaseGroupPatterns.Should().Contain("DarkSideMux");
        
        // Should include ARM32 performance requirements
        architecture.Performance.Should().NotBeNull();
        architecture.Performance.MaxMemoryUsageMB.Should().BeLessOrEqualTo(200);
        architecture.Performance.MaxPredictionLatencyMs.Should().BeLessOrEqualTo(500);
    }

    [Theory]
    [InlineData(AlgorithmType.LightGBM)]
    [InlineData(AlgorithmType.FastTree)]
    [InlineData(AlgorithmType.LogisticRegression)]
    public async Task CreateClassificationModelAsync_WithDifferentAlgorithms_ReturnsSuccessWithCorrectAlgorithm(AlgorithmType algorithmType)
    {
        // Given - Configuration with specific algorithm
        var modelConfig = new ModelConfiguration
        {
            ModelType = ModelType.MultiClassClassification,
            AlgorithmType = algorithmType
        };

        // When - Creating model with specific algorithm
        var result = await _service.CreateClassificationModelAsync(modelConfig);

        // Then - Should succeed with correct algorithm configuration
        result.IsSuccess.Should().BeTrue();
        
        var architecture = result.Value;
        architecture.Algorithm.AlgorithmType.Should().Be(algorithmType);
        architecture.Algorithm.IsValid.Should().BeTrue();
        architecture.Algorithm.Hyperparameters.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateClassificationModelAsync_WithCustomHyperparameters_MergesCorrectly()
    {
        // Given - Configuration with custom hyperparameters
        var customHyperparameters = new Dictionary<string, object>
        {
            ["custom_param"] = 42,
            ["learning_rate"] = 0.05 // Override default
        };
        
        var modelConfig = new ModelConfiguration
        {
            ModelType = ModelType.MultiClassClassification,
            AlgorithmType = AlgorithmType.LightGBM,
            CustomHyperparameters = customHyperparameters
        };

        // When - Creating model with custom parameters
        var result = await _service.CreateClassificationModelAsync(modelConfig);

        // Then - Should merge custom parameters with defaults
        result.IsSuccess.Should().BeTrue();
        
        var architecture = result.Value;
        architecture.Algorithm.Hyperparameters.Should().ContainKey("custom_param");
        architecture.Algorithm.Hyperparameters["custom_param"].Should().Be(42);
        architecture.Algorithm.LearningRate.Should().Be(0.1); // Service level learning rate unchanged
    }

    [Fact]
    public async Task CreateClassificationModelAsync_WithBinaryClassification_ReturnsFailure()
    {
        // Given - Binary classification configuration (not supported)
        var modelConfig = new ModelConfiguration
        {
            ModelType = ModelType.BinaryClassification,
            AlgorithmType = AlgorithmType.LightGBM
        };

        // When - Creating binary classification model
        var result = await _service.CreateClassificationModelAsync(modelConfig);

        // Then - Should fail with appropriate error
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("multi-class classification");
    }

    [Fact]
    public async Task ConfigureFeaturePipelineAsync_WithValidConfiguration_ReturnsSuccess()
    {
        // Given - Valid feature pipeline configuration
        var featureConfig = new FeaturePipelineConfiguration
        {
            NormalizeFeatures = true,
            MaxFeatures = 150,
            FeatureImportanceThreshold = 0.005
        };

        // When - Configuring feature pipeline
        var result = await _service.ConfigureFeaturePipelineAsync(featureConfig);

        // Then - Should succeed with correct configuration
        result.IsSuccess.Should().BeTrue();
        
        var pipelineConfig = result.Value;
        pipelineConfig.Should().NotBeNull();
        pipelineConfig.IsValid.Should().BeTrue();
        pipelineConfig.FeatureSelection.MaxFeatures.Should().Be(150);
        pipelineConfig.FeatureSelection.ImportanceThreshold.Should().Be(0.005);
        pipelineConfig.Normalization.NormalizeNumerical.Should().BeTrue();
    }

    [Fact]
    public async Task DefineCrossValidationStrategyAsync_WithValidConfiguration_ReturnsSuccess()
    {
        // Given - Valid cross-validation configuration
        var validationConfig = new ValidationConfiguration
        {
            Folds = 5,
            UseStratification = true,
            RandomSeed = 123
        };

        // When - Defining cross-validation strategy
        var result = await _service.DefineCrossValidationStrategyAsync(validationConfig);

        // Then - Should succeed with stratified configuration
        result.IsSuccess.Should().BeTrue();
        
        var crossValidation = result.Value;
        crossValidation.Should().NotBeNull();
        crossValidation.Folds.Should().Be(5);
        crossValidation.UseStratification.Should().BeTrue();
        crossValidation.RandomSeed.Should().Be(123);
        crossValidation.SamplingStrategy.Should().Be(ValidationSamplingStrategy.Stratified);
    }

    [Theory]
    [InlineData(1)] // Too few folds
    [InlineData(15)] // Too many folds
    public async Task DefineCrossValidationStrategyAsync_WithInvalidFolds_ReturnsFailure(int folds)
    {
        // Given - Invalid fold configuration
        var validationConfig = new ValidationConfiguration
        {
            Folds = folds,
            UseStratification = true,
            RandomSeed = 42
        };

        // When - Defining cross-validation with invalid folds
        var result = await _service.DefineCrossValidationStrategyAsync(validationConfig);

        // Then - Should fail with validation error
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("between 2 and 10");
    }

    [Fact]
    public async Task DefineEvaluationMetricsAsync_WithValidConfiguration_ReturnsSuccess()
    {
        // Given - Valid evaluation metrics configuration
        var metricsConfig = new EvaluationMetricsConfiguration
        {
            TargetAccuracy = 0.88,
            GenerateConfusionMatrix = true,
            CalculatePerCategoryMetrics = true
        };

        // When - Defining evaluation metrics
        var result = await _service.DefineEvaluationMetricsAsync(metricsConfig);

        // Then - Should succeed with comprehensive metrics
        result.IsSuccess.Should().BeTrue();
        
        var metrics = result.Value;
        metrics.Should().NotBeNull();
        metrics.TargetAccuracy.Should().Be(0.88);
        metrics.MinimumF1Score.Should().BeApproximately(0.78, 0.1); // Should be close to target accuracy
        metrics.GenerateConfusionMatrix.Should().BeTrue();
        metrics.CalculatePerCategoryMetrics.Should().BeTrue();
        
        // Should include primary and secondary metrics
        metrics.PrimaryMetrics.Should().NotBeEmpty();
        metrics.PrimaryMetrics.Should().Contain(EvaluationMetricType.Accuracy);
        metrics.SecondaryMetrics.Should().NotBeEmpty();
        metrics.SecondaryMetrics.Should().Contain(EvaluationMetricType.Precision);
    }

    [Theory]
    [InlineData(-0.1)] // Negative accuracy
    [InlineData(1.5)]  // Accuracy > 1.0
    public async Task DefineEvaluationMetricsAsync_WithInvalidAccuracy_ReturnsFailure(double targetAccuracy)
    {
        // Given - Invalid target accuracy
        var metricsConfig = new EvaluationMetricsConfiguration
        {
            TargetAccuracy = targetAccuracy,
            GenerateConfusionMatrix = true,
            CalculatePerCategoryMetrics = true
        };

        // When - Defining metrics with invalid accuracy
        var result = await _service.DefineEvaluationMetricsAsync(metricsConfig);

        // Then - Should fail with validation error
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("between 0.0 and 1.0");
    }

    [Fact]
    public async Task ValidateModelArchitectureAsync_WithValidArchitecture_ReturnsSuccess()
    {
        // Given - Valid model architecture
        var architecture = MLModelArchitecture.CreateRecommendedArchitecture();

        // When - Validating architecture
        var result = await _service.ValidateModelArchitectureAsync(architecture);

        // Then - Should pass validation
        result.IsSuccess.Should().BeTrue();
        
        var validationResult = result.Value;
        validationResult.Should().NotBeNull();
        validationResult.IsProductionReady.Should().BeTrue();
        
        // Should have minimal issues for recommended architecture
        var criticalIssues = validationResult.Issues.Where(i => i.Severity == IssueSeverity.Critical);
        criticalIssues.Should().BeEmpty();
    }

    [Fact]
    public async Task EstimateResourceRequirementsAsync_WithLightGBMArchitecture_ReturnsReasonableEstimates()
    {
        // Given - LightGBM architecture
        var architecture = MLModelArchitecture.CreateRecommendedArchitecture();

        // When - Estimating resource requirements
        var result = await _service.EstimateResourceRequirementsAsync(architecture);

        // Then - Should return reasonable estimates
        result.IsSuccess.Should().BeTrue();
        
        var estimate = result.Value;
        estimate.Should().NotBeNull();
        
        // Estimates should be reasonable for ARM32
        estimate.EstimatedMemoryMB.Should().BeGreaterThan(0);
        estimate.EstimatedMemoryMB.Should().BeLessOrEqualTo(300); // Conservative for ARM32
        estimate.EstimatedLatencyMs.Should().BeGreaterThan(0);
        estimate.EstimatedLatencyMs.Should().BeLessOrEqualTo(1000); // Reasonable prediction time
        estimate.EstimatedModelSizeMB.Should().BeGreaterThan(0);
        estimate.EstimatedModelSizeMB.Should().BeLessOrEqualTo(100); // Reasonable model size
        estimate.EstimatedTrainingTimeMinutes.Should().BeGreaterThan(0);
        
        // Should meet ARM32 performance requirements
        var meetsRequirements = estimate.MeetsRequirements(architecture.Performance);
        meetsRequirements.Should().BeTrue();
    }

    [Fact]
    public async Task GetRecommendedArchitectureAsync_ReturnsOptimalConfiguration()
    {
        // Given - Service ready for recommendation

        // When - Getting recommended architecture
        var result = await _service.GetRecommendedArchitectureAsync();

        // Then - Should return optimal architecture for Italian content
        result.IsSuccess.Should().BeTrue();
        
        var architecture = result.Value;
        architecture.Should().NotBeNull();
        architecture.IsValid.Should().BeTrue();
        
        // Should be optimized for Italian content
        architecture.ModelType.Should().Be(ModelType.MultiClassClassification);
        architecture.Algorithm.AlgorithmType.Should().Be(AlgorithmType.LightGBM);
        architecture.ItalianOptimization.ReleaseGroupPatterns.Should().Contain("NovaRip");
        architecture.ItalianOptimization.LanguageIndicators.Should().Contain("ITA");
        
        // Should meet ARM32 requirements
        architecture.Performance.MaxMemoryUsageMB.Should().BeLessOrEqualTo(200);
        architecture.Performance.MaxPredictionLatencyMs.Should().BeLessOrEqualTo(500);
        
        // Should include resource estimate
        architecture.ResourceEstimate.Should().NotBeNull();
        
        // Should use stratified cross-validation
        architecture.CrossValidation.UseStratification.Should().BeTrue();
        architecture.CrossValidation.Folds.Should().Be(5);
    }

    [Fact]
    public async Task ResourceEstimate_ForDifferentAlgorithms_VariesAppropriately()
    {
        // Given - Different algorithm configurations
        var lightGBMConfig = new ModelConfiguration
        {
            ModelType = ModelType.MultiClassClassification,
            AlgorithmType = AlgorithmType.LightGBM
        };
        
        var logisticConfig = new ModelConfiguration
        {
            ModelType = ModelType.MultiClassClassification,
            AlgorithmType = AlgorithmType.LogisticRegression
        };

        // When - Creating models with different algorithms
        var lightGBMResult = await _service.CreateClassificationModelAsync(lightGBMConfig);
        var logisticResult = await _service.CreateClassificationModelAsync(logisticConfig);

        // Then - Resource estimates should differ appropriately
        lightGBMResult.IsSuccess.Should().BeTrue();
        logisticResult.IsSuccess.Should().BeTrue();
        
        var lightGBMArchitecture = lightGBMResult.Value;
        var logisticArchitecture = logisticResult.Value;
        
        // LightGBM typically uses more memory but similar latency
        if (lightGBMArchitecture.ResourceEstimate != null && logisticArchitecture.ResourceEstimate != null)
        {
            lightGBMArchitecture.ResourceEstimate.EstimatedMemoryMB
                .Should().BeGreaterThan(logisticArchitecture.ResourceEstimate.EstimatedMemoryMB);
        }
    }
}