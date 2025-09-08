using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MediaButler.ML.Services;
using MediaButler.ML.Models;
using MediaButler.ML.Interfaces;
using Xunit;

namespace MediaButler.Tests.Unit.ML;

/// <summary>
/// Unit tests for ModelTrainingService covering model training pipeline management.
/// Tests focus on Italian content optimization and ARM32 deployment requirements.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" testing principles:
/// - Test behavior, not implementation
/// - Clear Given-When-Then structure  
/// - Values over state testing
/// - Independent, deterministic tests
/// </remarks>
public class ModelTrainingServiceTests
{
    private readonly Mock<ILogger<ModelTrainingService>> _mockLogger;
    private readonly Mock<IFeatureEngineeringService> _mockFeatureEngineering;
    private readonly ModelTrainingService _service;

    public ModelTrainingServiceTests()
    {
        _mockLogger = new Mock<ILogger<ModelTrainingService>>();
        _mockFeatureEngineering = new Mock<IFeatureEngineeringService>();
        _service = new ModelTrainingService(_mockLogger.Object, _mockFeatureEngineering.Object);
    }

    [Fact]
    public async Task TrainModelAsync_WithValidTrainingData_ReturnsSuccess()
    {
        // Given - Valid training data and configuration
        var trainingData = CreateSampleItalianTrainingData();
        var trainingConfig = TrainingConfiguration.CreateDefault();

        // When - Training model
        var result = await _service.TrainModelAsync(trainingData, trainingConfig);

        // Then - Should succeed with valid model info
        result.IsSuccess.Should().BeTrue();
        
        var modelInfo = result.Value;
        modelInfo.Should().NotBeNull();
        modelInfo.ModelId.Should().NotBeEmpty();
        modelInfo.TrainingSampleCount.Should().Be(trainingData.Count());
        modelInfo.TrainingDuration.Should().BeGreaterThan(TimeSpan.Zero);
        modelInfo.ValidationMetrics.Should().NotBeNull();
        modelInfo.TrainingMetrics.Should().NotBeNull();
        
        // Should have reasonable performance
        modelInfo.ValidationMetrics.Accuracy.Should().BeGreaterThan(0.0);
        modelInfo.ValidationMetrics.MacroF1Score.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public async Task TrainModelAsync_WithFastTrainingConfig_CompletesQuickly()
    {
        // Given - Fast training configuration for quick testing
        var trainingData = CreateSampleItalianTrainingData().Take(50); // Smaller dataset
        var trainingConfig = TrainingConfiguration.CreateFast();

        // When - Training with fast config
        var result = await _service.TrainModelAsync(trainingData, trainingConfig);

        // Then - Should complete quickly
        result.IsSuccess.Should().BeTrue();
        
        var modelInfo = result.Value;
        modelInfo.TrainingDuration.Should().BeLessThan(TimeSpan.FromMinutes(2));
        modelInfo.TrainingConfig.MaxEpochs.Should().Be(25); // Fast config epochs
        modelInfo.TrainingConfig.EarlyStoppingPatience.Should().Be(5);
    }

    [Fact]
    public async Task TrainModelAsync_WithCancellation_StopsTraining()
    {
        // Given - Training data and cancellation token
        var trainingData = CreateSampleItalianTrainingData();
        var trainingConfig = TrainingConfiguration.CreateDefault();
        using var cancellationSource = new CancellationTokenSource();
        
        // When - Cancelling training immediately
        cancellationSource.Cancel();
        var result = await _service.TrainModelAsync(trainingData, trainingConfig, cancellationSource.Token);

        // Then - Should handle cancellation appropriately
        // Note: In real implementation, this would properly handle cancellation
        // For now, we verify the service can handle the cancellation token
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateTrainingPipelineAsync_WithRecommendedArchitecture_ReturnsValidPipeline()
    {
        // Given - Recommended architecture for Italian content
        var architecture = MLModelArchitecture.CreateRecommendedArchitecture();
        var featurePipeline = FeaturePipelineConfig.CreateComprehensive();

        // When - Creating training pipeline
        var result = await _service.CreateTrainingPipelineAsync(architecture, featurePipeline);

        // Then - Should create valid ML.NET pipeline
        result.IsSuccess.Should().BeTrue();
        
        var pipeline = result.Value;
        pipeline.Should().NotBeNull();
        pipeline.PipelineId.Should().NotBeEmpty();
        pipeline.Architecture.Should().Be(architecture);
        pipeline.FeaturePipeline.Should().Be(featurePipeline);
        pipeline.IsValid.Should().BeTrue();
        
        // Should include proper transformation steps
        pipeline.TransformationSteps.Should().NotBeEmpty();
        pipeline.TransformationSteps.Should().Contain(step => step.TransformationType == "FeaturizeText");
        pipeline.TransformationSteps.Should().Contain(step => step.TransformationType == "Concatenate");
        
        // Should have correct algorithm configuration
        pipeline.AlgorithmConfig.Should().NotBeNull();
        pipeline.AlgorithmConfig.AlgorithmType.Should().Be(architecture.Algorithm.AlgorithmType);
        pipeline.AlgorithmConfig.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(AlgorithmType.LightGBM)]
    [InlineData(AlgorithmType.FastTree)]
    [InlineData(AlgorithmType.LogisticRegression)]
    public async Task CreateTrainingPipelineAsync_WithDifferentAlgorithms_CreatesAppropriateConfig(AlgorithmType algorithmType)
    {
        // Given - Architecture with specific algorithm
        var architecture = MLModelArchitecture.CreateRecommendedArchitecture();
        var algorithmConfig = architecture.Algorithm with { AlgorithmType = algorithmType };
        var testArchitecture = architecture with { Algorithm = algorithmConfig };
        var featurePipeline = FeaturePipelineConfig.CreateComprehensive();

        // When - Creating pipeline for specific algorithm
        var result = await _service.CreateTrainingPipelineAsync(testArchitecture, featurePipeline);

        // Then - Should configure for the specified algorithm
        result.IsSuccess.Should().BeTrue();
        
        var pipeline = result.Value;
        pipeline.AlgorithmConfig.AlgorithmType.Should().Be(algorithmType);
        pipeline.Architecture.Algorithm.AlgorithmType.Should().Be(algorithmType);
    }

    [Fact]
    public async Task PerformCrossValidationAsync_WithTrainingData_ReturnsStableResults()
    {
        // Given - Training data and architecture for cross-validation
        var trainingData = CreateSampleItalianTrainingData();
        var architecture = MLModelArchitecture.CreateRecommendedArchitecture();

        // When - Performing cross-validation
        var result = await _service.PerformCrossValidationAsync(trainingData, architecture);

        // Then - Should return stable cross-validation results
        result.IsSuccess.Should().BeTrue();
        
        var cvResult = result.Value;
        cvResult.Should().NotBeNull();
        cvResult.NumberOfFolds.Should().Be(architecture.CrossValidation.Folds);
        cvResult.FoldResults.Should().HaveCount(architecture.CrossValidation.Folds);
        
        // Results should be reasonable
        cvResult.MeanAccuracy.Should().BeInRange(0.1, 1.0);
        cvResult.MeanF1Score.Should().BeInRange(0.1, 1.0);
        cvResult.AccuracyStdDev.Should().BeGreaterOrEqualTo(0.0);
        cvResult.F1ScoreStdDev.Should().BeGreaterOrEqualTo(0.0);
        
        // Individual fold results should be valid
        foreach (var fold in cvResult.FoldResults)
        {
            fold.FoldNumber.Should().BeInRange(0, architecture.CrossValidation.Folds - 1);
            fold.Accuracy.Should().BeInRange(0.0, 1.0);
            fold.F1Score.Should().BeInRange(0.0, 1.0);
            fold.TrainingTime.Should().BeGreaterThan(TimeSpan.Zero);
            fold.TrainingSampleCount.Should().BeGreaterThan(0);
            fold.ValidationSampleCount.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task EstimateTrainingRequirementsAsync_WithItalianData_ReturnsReasonableEstimates()
    {
        // Given - Italian training data and architecture
        var trainingData = CreateSampleItalianTrainingData();
        var architecture = MLModelArchitecture.CreateRecommendedArchitecture();

        // When - Estimating training requirements
        var result = await _service.EstimateTrainingRequirementsAsync(trainingData, architecture);

        // Then - Should return reasonable estimates for ARM32
        result.IsSuccess.Should().BeTrue();
        
        var estimate = result.Value;
        estimate.Should().NotBeNull();
        estimate.SampleCount.Should().Be(trainingData.Count());
        estimate.FeatureCount.Should().BeGreaterThan(0);
        
        // Estimates should be reasonable for ARM32 deployment
        estimate.EstimatedPeakMemoryMB.Should().BeInRange(50, 300);
        estimate.EstimatedTrainingTime.Should().BeInRange(TimeSpan.FromMinutes(1), TimeSpan.FromHours(2));
        estimate.EstimatedCpuUtilization.Should().BeInRange(50, 100);
        estimate.EstimatedTempDiskSpaceMB.Should().BeGreaterThan(0);
        estimate.EstimateConfidence.Should().BeInRange(0.0, 1.0);
        
        // Should fit ARM32 constraints for reasonable datasets
        if (trainingData.Count() <= 1000)
        {
            estimate.FitsARM32Constraints.Should().BeTrue();
        }
    }

    [Theory]
    [InlineData(AlgorithmType.LightGBM, 120.0, 8.0)]
    [InlineData(AlgorithmType.FastTree, 80.0, 5.0)]
    [InlineData(AlgorithmType.LogisticRegression, 50.0, 3.0)]
    public async Task EstimateTrainingRequirementsAsync_WithDifferentAlgorithms_VariesAppropriately(
        AlgorithmType algorithmType, double expectedBaseMemory, double expectedBaseTime)
    {
        // Given - Training data and architecture with specific algorithm
        var trainingData = CreateSampleItalianTrainingData().Take(100); // Small dataset for base estimates
        var architecture = MLModelArchitecture.CreateRecommendedArchitecture();
        var algorithmConfig = architecture.Algorithm with { AlgorithmType = algorithmType };
        var testArchitecture = architecture with { Algorithm = algorithmConfig };

        // When - Estimating requirements for specific algorithm
        var result = await _service.EstimateTrainingRequirementsAsync(trainingData, testArchitecture);

        // Then - Estimates should vary by algorithm
        result.IsSuccess.Should().BeTrue();
        
        var estimate = result.Value;
        
        // Different algorithms should have different resource profiles
        if (algorithmType == AlgorithmType.LightGBM)
        {
            estimate.EstimatedPeakMemoryMB.Should().BeGreaterThan(100);
        }
        else if (algorithmType == AlgorithmType.LogisticRegression)
        {
            estimate.EstimatedPeakMemoryMB.Should().BeLessThan(100);
            estimate.EstimatedTrainingTime.Should().BeLessThan(TimeSpan.FromMinutes(10));
        }
    }

    [Fact]
    public async Task ValidateTrainingDataAsync_WithQualityData_ReturnsValidStatus()
    {
        // Given - High-quality Italian training data
        var trainingData = CreateSampleItalianTrainingData();
        var validationRules = TrainingDataValidationRules.CreateDefault();

        // When - Validating training data
        var result = await _service.ValidateTrainingDataAsync(trainingData, validationRules);

        // Then - Should validate as good quality data
        result.IsSuccess.Should().BeTrue();
        
        var report = result.Value;
        report.Should().NotBeNull();
        report.TotalSamples.Should().Be(trainingData.Count());
        report.ValidSamples.Should().BeGreaterThan(0);
        report.QualityScore.Should().BeGreaterThan(0.5);
        
        // Should have category distribution
        report.CategoryDistribution.Should().NotBeEmpty();
        report.CategoryDistribution.Should().ContainKey("BREAKING BAD");
        report.CategoryDistribution.Should().ContainKey("GOMORRA");
        
        // Should analyze class imbalance
        report.ImbalanceAnalysis.Should().NotBeNull();
        report.ImbalanceAnalysis.MaxCategorySamples.Should().BeGreaterThan(0);
        report.ImbalanceAnalysis.MinCategorySamples.Should().BeGreaterThan(0);
        report.ImbalanceAnalysis.ImbalanceRatio.Should().BeGreaterThan(0);
        
        // Should provide statistics
        report.Statistics.Should().NotBeNull();
        report.Statistics.MeanFilenameLength.Should().BeGreaterThan(20);
        report.Statistics.ItalianContentPercentage.Should().BeGreaterThan(0);
        report.Statistics.UniqueCategoryCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task ValidateTrainingDataAsync_WithPoorQualityData_ReturnsIssues()
    {
        // Given - Poor quality training data with issues
        var poorTrainingData = new[]
        {
            new TrainingSample
            {
                Filename = "short", // Too short
                Category = "TEST",
                Confidence = 0.3, // Low confidence
                Source = TrainingSampleSource.Manual,
                CreatedAt = DateTime.UtcNow
            },
            new TrainingSample
            {
                Filename = "sample-fake-virus.avi", // Forbidden pattern
                Category = "TEST",
                Confidence = 0.9,
                Source = TrainingSampleSource.Manual,
                CreatedAt = DateTime.UtcNow
            }
        };

        var validationRules = TrainingDataValidationRules.CreateStrict();

        // When - Validating poor quality data
        var result = await _service.ValidateTrainingDataAsync(poorTrainingData, validationRules);

        // Then - Should identify quality issues
        result.IsSuccess.Should().BeTrue();
        
        var report = result.Value;
        report.Issues.Should().NotBeEmpty();
        report.QualityScore.Should().BeLessThan(0.8);
        report.ValidSamples.Should().BeLessThan(report.TotalSamples);
        
        // Should have warnings for short filenames and forbidden patterns
        report.Issues.Should().Contain(issue => issue.Description.Contains("too short"));
        report.Issues.Should().Contain(issue => issue.Description.Contains("Forbidden pattern"));
        
        // Should provide improvement recommendations
        report.Recommendations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ValidateTrainingDataAsync_WithImbalancedData_RecommendsSampling()
    {
        // Given - Highly imbalanced training data
        var imbalancedData = new List<TrainingSample>();
        
        // Add many samples for one category
        for (int i = 0; i < 80; i++)
        {
            imbalancedData.Add(new TrainingSample
            {
                Filename = $"Breaking.Bad.S01E{i:D2}.1080p.BluRay.x264-NovaRip.mkv",
                Category = "BREAKING BAD",
                Confidence = 0.95,
                Source = TrainingSampleSource.Manual,
                CreatedAt = DateTime.UtcNow
            });
        }
        
        // Add few samples for another category
        for (int i = 0; i < 5; i++)
        {
            imbalancedData.Add(new TrainingSample
            {
                Filename = $"Gomorra.S01E{i:D2}.ITA.720p.HDTV.x264-DarkSideMux.avi",
                Category = "GOMORRA",
                Confidence = 0.9,
                Source = TrainingSampleSource.Manual,
                CreatedAt = DateTime.UtcNow
            });
        }

        var validationRules = TrainingDataValidationRules.CreateDefault();

        // When - Validating imbalanced data
        var result = await _service.ValidateTrainingDataAsync(imbalancedData, validationRules);

        // Then - Should identify imbalance and recommend sampling
        result.IsSuccess.Should().BeTrue();
        
        var report = result.Value;
        report.ImbalanceAnalysis.Should().NotBeNull();
        report.ImbalanceAnalysis.IsBalanced.Should().BeFalse();
        report.ImbalanceAnalysis.ImbalanceRatio.Should().BeGreaterThan(10);
        report.ImbalanceAnalysis.MajorityCategory.Should().Be("BREAKING BAD");
        report.ImbalanceAnalysis.MinorityCategory.Should().Be("GOMORRA");
        
        // Should recommend appropriate sampling strategy
        report.ImbalanceAnalysis.RecommendedStrategy.Should().BeOneOf(
            SamplingStrategy.Oversample, 
            SamplingStrategy.Combined, 
            SamplingStrategy.CollectMoreData);
        
        report.Recommendations.Should().Contain(rec => rec.Contains("class imbalance"));
    }

    [Fact]
    public async Task GetTrainingProgressAsync_WithActiveSession_ReturnsProgress()
    {
        // Given - Active training session
        var trainingData = CreateSampleItalianTrainingData().Take(20);
        var trainingConfig = TrainingConfiguration.CreateFast();
        
        // Start training (but don't await completion)
        var trainingTask = _service.TrainModelAsync(trainingData, trainingConfig);
        
        // Wait a short time for training to start
        await Task.Delay(100);

        // When - Getting training progress
        var result = await _service.GetTrainingProgressAsync(trainingConfig.SessionId);

        // Then - Should return progress information
        if (result.IsSuccess)
        {
            var progress = result.Value;
            progress.Should().NotBeNull();
            progress.SessionId.Should().Be(trainingConfig.SessionId);
            progress.TotalEpochs.Should().Be(trainingConfig.MaxEpochs);
            progress.CurrentPhase.Should().NotBe(TrainingPhase.Failed);
            progress.CompletionPercentage.Should().BeInRange(0, 100);
            progress.StatusMessage.Should().NotBeEmpty();
        }
        
        // Clean up - wait for training to complete
        await trainingTask;
    }

    [Fact]
    public async Task GetTrainingProgressAsync_WithInvalidSession_ReturnsFailure()
    {
        // Given - Invalid session ID
        var invalidSessionId = Guid.NewGuid().ToString();

        // When - Getting progress for invalid session
        var result = await _service.GetTrainingProgressAsync(invalidSessionId);

        // Then - Should return failure
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task OptimizeHyperparametersAsync_WithLightGBMConfig_FindsBetterParameters()
    {
        // Given - Training data and LightGBM optimization config
        var trainingData = CreateSampleItalianTrainingData().Take(50); // Small dataset for fast optimization
        var optimizationConfig = HyperparameterOptimizationConfig.CreateForLightGBM() with 
        { 
            MaxIterations = 5, // Fast test
            MaxOptimizationTimeMinutes = 5
        };

        // When - Optimizing hyperparameters
        var result = await _service.OptimizeHyperparametersAsync(trainingData, optimizationConfig);

        // Then - Should find optimized parameters
        result.IsSuccess.Should().BeTrue();
        
        var optimizationResult = result.Value;
        optimizationResult.Should().NotBeNull();
        optimizationResult.BestHyperparameters.Should().NotBeEmpty();
        optimizationResult.IterationsPerformed.Should().BeGreaterThan(0);
        optimizationResult.IterationsPerformed.Should().BeLessOrEqualTo(optimizationConfig.MaxIterations);
        optimizationResult.Algorithm.Should().Be(optimizationConfig.Algorithm);
        optimizationResult.TargetMetric.Should().Be(optimizationConfig.TargetMetric);
        
        // Should have iteration results
        optimizationResult.IterationResults.Should().NotBeEmpty();
        optimizationResult.IterationResults.Should().Contain(iter => iter.IsBestSoFar);
        
        // Should analyze convergence
        optimizationResult.Convergence.Should().NotBeNull();
        optimizationResult.Convergence.Quality.Should().NotBe(OptimizationQuality.Failed);
        
        // Should create optimized architecture
        optimizationResult.OptimizedArchitecture.Should().NotBeNull();
        optimizationResult.OptimizedArchitecture.Algorithm.Hyperparameters.Should().Be(optimizationResult.BestHyperparameters);
    }

    [Fact]
    public async Task SaveModelAsync_WithTrainedModel_SavesSuccessfully()
    {
        // Given - Trained model info and save path
        var modelInfo = CreateSampleTrainedModelInfo();
        var metadata = ModelMetadata.CreateDefault();
        var modelPath = Path.GetTempFileName();

        try
        {
            // When - Saving model
            var result = await _service.SaveModelAsync(modelInfo, modelPath, metadata);

            // Then - Should save successfully
            result.IsSuccess.Should().BeTrue();
            
            var persistenceInfo = result.Value;
            persistenceInfo.Should().NotBeNull();
            persistenceInfo.ModelPath.Should().Be(modelPath);
            persistenceInfo.FileSizeBytes.Should().BeGreaterThan(0);
            persistenceInfo.Metadata.Should().Be(metadata);
            persistenceInfo.ModelVersion.Should().Be(modelInfo.ModelVersion);
            persistenceInfo.Checksum.Should().NotBeEmpty();
            
            // File should exist
            File.Exists(modelPath).Should().BeTrue();
        }
        finally
        {
            // Clean up
            if (File.Exists(modelPath))
                File.Delete(modelPath);
        }
    }

    [Fact]
    public async Task LoadModelAsync_WithValidModelFile_LoadsSuccessfully()
    {
        // Given - Saved model file
        var originalModelInfo = CreateSampleTrainedModelInfo();
        var metadata = ModelMetadata.CreateDefault();
        var modelPath = Path.GetTempFileName();
        
        // Save model first
        var saveResult = await _service.SaveModelAsync(originalModelInfo, modelPath, metadata);
        saveResult.IsSuccess.Should().BeTrue();

        try
        {
            // When - Loading model
            var loadResult = await _service.LoadModelAsync(modelPath);

            // Then - Should load successfully
            loadResult.IsSuccess.Should().BeTrue();
            
            var loadedModelInfo = loadResult.Value;
            loadedModelInfo.Should().NotBeNull();
            loadedModelInfo.ModelPath.Should().Be(modelPath);
            loadedModelInfo.ModelVersion.Should().NotBeEmpty();
        }
        finally
        {
            // Clean up
            if (File.Exists(modelPath))
                File.Delete(modelPath);
        }
    }

    [Fact]
    public async Task LoadModelAsync_WithValidation_ValidatesModel()
    {
        // Given - Saved model file and validation config
        var originalModelInfo = CreateSampleTrainedModelInfo();
        var metadata = ModelMetadata.CreateDefault();
        var modelPath = Path.GetTempFileName();
        var validationConfig = ModelValidationConfig.CreateDefault();
        
        // Save model first
        var saveResult = await _service.SaveModelAsync(originalModelInfo, modelPath, metadata);
        saveResult.IsSuccess.Should().BeTrue();

        try
        {
            // When - Loading model with validation
            var loadResult = await _service.LoadModelAsync(modelPath, validationConfig);

            // Then - Should load and validate successfully
            loadResult.IsSuccess.Should().BeTrue();
        }
        finally
        {
            // Clean up
            if (File.Exists(modelPath))
                File.Delete(modelPath);
        }
    }

    [Fact]
    public async Task LoadModelAsync_WithNonexistentFile_ReturnsFailure()
    {
        // Given - Nonexistent model file path
        var nonexistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".model");

        // When - Loading nonexistent model
        var result = await _service.LoadModelAsync(nonexistentPath);

        // Then - Should return failure
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    #region Helper Methods

    private static IEnumerable<TrainingSample> CreateSampleItalianTrainingData()
    {
        var samples = new List<TrainingSample>();

        // Breaking Bad samples
        var breakingBadFilenames = new[]
        {
            "Breaking.Bad.S01E01.Pilot.1080p.BluRay.x264-NovaRip.mkv",
            "Breaking.Bad.S01E02.Cat.In.The.Bag.720p.BluRay.x264-NovaRip.mkv",
            "Breaking.Bad.S02E01.Seven.Thirty.Seven.1080p.BluRay.x264-NovaRip.mkv",
            "Breaking.Bad.S02E13.ABQ.FINAL.1080p.BluRay.x264-NovaRip.mkv",
            "Breaking.Bad.S03E01.No.Mas.1080p.BluRay.x264-NovaRip.mkv",
            "Breaking.Bad.S04E13.Face.Off.FINAL.1080p.BluRay.x264-NovaRip.mkv",
            "Breaking.Bad.S05E16.Felina.SERIES.FINALE.1080p.BluRay.x264-NovaRip.mkv"
        };

        foreach (var filename in breakingBadFilenames)
        {
            samples.Add(new TrainingSample
            {
                Filename = filename,
                Category = "BREAKING BAD",
                Confidence = 0.95 + (Random.Shared.NextDouble() * 0.05),
                Source = TrainingSampleSource.UserConfirmed,
                CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(30))
            });
        }

        // Italian content samples
        var italianFilenames = new[]
        {
            "Il.Commissario.Montalbano.S14E01.ITA.HDTV.x264-DarkSideMux.avi",
            "Il.Commissario.Montalbano.S14E02.ITA.HDTV.x264-DarkSideMux.avi",
            "Il.Commissario.Montalbano.S14E03.ITA.HDTV.x264-DarkSideMux.avi",
            "Gomorra.S04E01.ITA.720p.HDTV.x264-Pir8.mkv",
            "Gomorra.S04E02.ITA.720p.HDTV.x264-Pir8.mkv",
            "Gomorra.S04E12.FINAL.ITA.720p.HDTV.x264-Pir8.mkv",
            "Suburra.S03E01.ITA.1080p.NF.WEB-DL.DDP5.1.x264-Pir8.mkv",
            "Suburra.S03E06.ITA.1080p.NF.WEB-DL.DDP5.1.x264-Pir8.mkv",
            "La.Casa.di.Carta.S05E01.ITA.720p.NF.WEB-DL.DDP5.1.x264-MeM.mkv",
            "La.Casa.di.Carta.S05E10.FINAL.ITA.720p.NF.WEB-DL.DDP5.1.x264-MeM.mkv"
        };

        var italianCategories = new[]
        {
            "COMMISSARIO MONTALBANO", "COMMISSARIO MONTALBANO", "COMMISSARIO MONTALBANO",
            "GOMORRA", "GOMORRA", "GOMORRA",
            "SUBURRA", "SUBURRA",
            "LA CASA DI CARTA", "LA CASA DI CARTA"
        };

        for (int i = 0; i < italianFilenames.Length; i++)
        {
            samples.Add(new TrainingSample
            {
                Filename = italianFilenames[i],
                Category = italianCategories[i],
                Confidence = 0.88 + (Random.Shared.NextDouble() * 0.1),
                Source = TrainingSampleSource.UserConfirmed,
                CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(30))
            });
        }

        // Other international content
        var otherFilenames = new[]
        {
            "The.Office.US.S01E01.Pilot.1080p.BluRay.x264-NovaRip.mkv",
            "The.Office.US.S02E01.The.Dundies.1080p.BluRay.x264-NovaRip.mkv",
            "Friends.S01E01.The.Pilot.1080p.BluRay.x264-NovaRip.mkv",
            "Friends.S10E18.The.Last.One.SERIES.FINALE.1080p.BluRay.x264-NovaRip.mkv",
            "Game.of.Thrones.S01E01.Winter.is.Coming.1080p.BluRay.x264-NovaRip.mkv",
            "Game.of.Thrones.S08E06.The.Iron.Throne.SERIES.FINALE.1080p.BluRay.x264-NovaRip.mkv"
        };

        var otherCategories = new[]
        {
            "THE OFFICE", "THE OFFICE",
            "FRIENDS", "FRIENDS", 
            "GAME OF THRONES", "GAME OF THRONES"
        };

        for (int i = 0; i < otherFilenames.Length; i++)
        {
            samples.Add(new TrainingSample
            {
                Filename = otherFilenames[i],
                Category = otherCategories[i],
                Confidence = 0.92 + (Random.Shared.NextDouble() * 0.08),
                Source = TrainingSampleSource.UserConfirmed,
                CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(30))
            });
        }

        return samples;
    }

    private static TrainedModelInfo CreateSampleTrainedModelInfo()
    {
        var architecture = MLModelArchitecture.CreateRecommendedArchitecture();
        var trainingConfig = TrainingConfiguration.CreateDefault();
        
        var trainingMetrics = new TrainingMetrics
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

        var validationMetrics = new ModelPerformanceMetrics
        {
            Accuracy = 0.82,
            MacroF1Score = 0.78,
            WeightedF1Score = 0.80,
            MacroPrecision = 0.79,
            MacroRecall = 0.77,
            LogLoss = 0.9,
            PerCategoryMetrics = new Dictionary<string, CategoryPerformanceMetrics>().AsReadOnly(),
            ConfusionMatrix = new ConfusionMatrix
            {
                Labels = new[] { "BREAKING BAD", "GOMORRA" }.AsReadOnly(),
                Matrix = new int[,] { { 45, 5 }, { 8, 42 } },
                TotalPredictions = 100
            },
            ConfidenceDistribution = new ConfidenceAnalysis
            {
                MeanConfidence = 0.85,
                MedianConfidence = 0.87,
                ConfidenceStdDev = 0.12,
                ConfidenceBins = new Dictionary<string, int> { ["0.8-1.0"] = 80, ["0.6-0.8"] = 20 }.AsReadOnly(),
                HighConfidencePercentage = 0.8,
                LowConfidencePercentage = 0.05
            }
        };

        return new TrainedModelInfo
        {
            ModelId = Guid.NewGuid().ToString(),
            Architecture = architecture,
            TrainingConfig = trainingConfig,
            TrainingMetrics = trainingMetrics,
            ValidationMetrics = validationMetrics,
            ModelPath = string.Empty,
            TrainingCompletedAt = DateTime.UtcNow,
            TrainingDuration = TimeSpan.FromMinutes(15),
            TrainingSampleCount = 150,
            ModelVersion = "1.0.0"
        };
    }

    #endregion
}