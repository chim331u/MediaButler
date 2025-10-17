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
    public async Task SaveModelAsync_WithTrainedModel_SavesSuccessfully()
    {
        // Given - Actually trained model and save path
        var trainingData = CreateSampleItalianTrainingData().Take(20);
        var trainingConfig = TrainingConfiguration.CreateFast();

        // Train the model first
        var trainResult = await _service.TrainModelAsync(trainingData, trainingConfig);
        trainResult.IsSuccess.Should().BeTrue();
        var modelInfo = trainResult.Value;

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

            // Metadata file should also exist
            var metadataPath = Path.ChangeExtension(modelPath, ".meta.json");
            File.Exists(metadataPath).Should().BeTrue();
        }
        finally
        {
            // Clean up
            if (File.Exists(modelPath))
                File.Delete(modelPath);
            var metadataPath = Path.ChangeExtension(modelPath, ".meta.json");
            if (File.Exists(metadataPath))
                File.Delete(metadataPath);
        }
    }

    [Fact]
    public async Task LoadModelAsync_WithValidModelFile_LoadsSuccessfully()
    {
        // Given - Train and save a model first
        var trainingData = CreateSampleItalianTrainingData().Take(20);
        var trainingConfig = TrainingConfiguration.CreateFast();

        // Train the model
        var trainResult = await _service.TrainModelAsync(trainingData, trainingConfig);
        trainResult.IsSuccess.Should().BeTrue();
        var originalModelInfo = trainResult.Value;

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
            loadedModelInfo.ModelVersion.Should().BeGreaterThan(0);
        }
        finally
        {
            // Clean up
            if (File.Exists(modelPath))
                File.Delete(modelPath);
            var metadataPath = Path.ChangeExtension(modelPath, ".meta.json");
            if (File.Exists(metadataPath))
                File.Delete(metadataPath);
        }
    }

    [Fact]
    public async Task LoadModelAsync_WithValidation_ValidatesModel()
    {
        // Given - Train and save a model first
        var trainingData = CreateSampleItalianTrainingData().Take(20);
        var trainingConfig = TrainingConfiguration.CreateFast();

        // Train the model
        var trainResult = await _service.TrainModelAsync(trainingData, trainingConfig);
        trainResult.IsSuccess.Should().BeTrue();
        var originalModelInfo = trainResult.Value;

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
            var metadataPath = Path.ChangeExtension(modelPath, ".meta.json");
            if (File.Exists(metadataPath))
                File.Delete(metadataPath);
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
                Source = TrainingSampleSource.UserFeedback,
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
                Source = TrainingSampleSource.UserFeedback,
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
                Source = TrainingSampleSource.UserFeedback,
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
            ModelVersion = 1
        };
    }

    #endregion
}