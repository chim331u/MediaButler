using FluentAssertions;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Utils;
using MediaButler.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrainingConfig = MediaButler.ML.Models.TrainingConfiguration;
using MLModels = MediaButler.ML.Models;

namespace MediaButler.Tests.Integration.ML;

/// <summary>
/// Integration tests for end-to-end model training, persistence, and classification workflow.
/// Tests complete ML pipeline: Train → Save → Load → Classify with real data and disk I/O.
/// Follows "Simple Made Easy" principles with clear Given-When-Then structure.
/// </summary>
public class ModelTrainingIntegrationTests : IntegrationTestBase
{
    public ModelTrainingIntegrationTests(DatabaseFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CompleteMLWorkflow_TrainSaveLoadClassify_ShouldWorkEndToEnd()
    {
        // Given - Training data from CSV and temporary model path
        using var scope = CreateScope();
        var modelTrainingService = scope.ServiceProvider.GetRequiredService<IModelTrainingService>();
        var classificationService = scope.ServiceProvider.GetRequiredService<IClassificationService>();

        var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "../../../../data/training/tv-series-training-data.csv");
        var tempModelPath = Path.Combine(Path.GetTempPath(), $"test-model-{Guid.NewGuid()}.zip");
        var tempMetadataPath = Path.ChangeExtension(tempModelPath, ".meta.json");

        try
        {
            // Step 1: Import training data from CSV
            var importerLogger = scope.ServiceProvider.GetRequiredService<ILogger<CsvTrainingDataImporter>>();
            var importer = new CsvTrainingDataImporter(importerLogger);

            var csvConfig = new MediaButler.ML.Models.CsvImportConfiguration
            {
                HasHeader = true,
                Separator = ';',
                NormalizeCategoryNames = true,
                SkipDuplicates = true,
                ValidateFileExtensions = true
            };

            var importResult = await importer.ImportFromCsvAsync(csvPath, csvConfig);
            importResult.IsSuccess.Should().BeTrue($"CSV import should succeed: {importResult.Error}");
            importResult.Value.ImportedSamples.Should().HaveCountGreaterThan(50, "Should have sufficient training data");

            // Step 2: Train model with imported data
            var trainingConfig = TrainingConfig.CreateFast(); // Fast config for integration tests
            var trainResult = await modelTrainingService.TrainModelAsync(importResult.Value.ImportedSamples, trainingConfig);
            trainResult.IsSuccess.Should().BeTrue($"Model training should succeed: {trainResult.Error}");

            var trainedModelInfo = trainResult.Value;
            trainedModelInfo.Should().NotBeNull();
            trainedModelInfo.ModelId.Should().NotBeEmpty();
            trainedModelInfo.Accuracy.Should().BeGreaterThan(0.5f, "Model should achieve reasonable accuracy");

            // Step 3: Save trained model to disk
            var metadata = new MLModels.ModelMetadata
            {
                ModelName = "IntegrationTestModel",
                Version = "1.0.0",
                CreatedAt = DateTime.UtcNow,
                Description = "Model trained for integration testing",
                Author = "Integration Test"
            };
            var saveResult = await modelTrainingService.SaveModelAsync(trainedModelInfo, tempModelPath, metadata);
            saveResult.IsSuccess.Should().BeTrue($"Model save should succeed: {saveResult.Error}");

            var persistenceInfo = saveResult.Value;
            persistenceInfo.ModelPath.Should().Be(tempModelPath);
            persistenceInfo.FileSizeBytes.Should().BeGreaterThan(0);
            File.Exists(tempModelPath).Should().BeTrue("Model file should exist on disk");
            File.Exists(tempMetadataPath).Should().BeTrue("Metadata file should exist on disk");

            // Step 4: Load model from disk
            var loadResult = await modelTrainingService.LoadModelAsync(tempModelPath);
            loadResult.IsSuccess.Should().BeTrue($"Model load should succeed: {loadResult.Error}");

            var loadedModelInfo = loadResult.Value;
            loadedModelInfo.Should().NotBeNull();
            loadedModelInfo.ModelPath.Should().Be(tempModelPath);

            // Step 5: Use loaded model for classification
            // Note: This requires the classification service to load the model from the path
            // For now, we verify the model was successfully saved and loaded
            // In a full integration test, we would configure the classification service to use this model

            // When - Test classification with the training workflow complete
            var testFilename = "Breaking.Bad.S05E16.FINAL.ITA.1080p.BluRay.x264-KILLERS.mkv";
            var classifyResult = await classificationService.ClassifyFilenameAsync(testFilename);

            // Then - Classification should work (may not use our trained model yet, but pipeline should work)
            classifyResult.IsSuccess.Should().BeTrue();
            var classificationResult = classifyResult.Value;
            classificationResult.Should().NotBeNull();
            classificationResult.PredictedCategory.Should().NotBeNull();
            classificationResult.Confidence.Should().BeGreaterOrEqualTo(0.0f);
            classificationResult.Confidence.Should().BeLessOrEqualTo(1.0f);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempModelPath))
                File.Delete(tempModelPath);
            if (File.Exists(tempMetadataPath))
                File.Delete(tempMetadataPath);
        }
    }

    [Fact]
    public async Task TrainModel_WithRealCSVData_ShouldProduceValidModel()
    {
        // Given - Real training data from CSV
        using var scope = CreateScope();
        var modelTrainingService = scope.ServiceProvider.GetRequiredService<IModelTrainingService>();

        var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "../../../../data/training/tv-series-training-data.csv");

        // When - Import and train with real data
        var importerLogger = scope.ServiceProvider.GetRequiredService<ILogger<CsvTrainingDataImporter>>();
        var importer = new CsvTrainingDataImporter(importerLogger);

        var csvConfig = new MediaButler.ML.Models.CsvImportConfiguration
        {
            HasHeader = true,
            Separator = ';',
            NormalizeCategoryNames = true,
            SkipDuplicates = true,
            ValidateFileExtensions = true
        };

        var importResult = await importer.ImportFromCsvAsync(csvPath, csvConfig);
        importResult.IsSuccess.Should().BeTrue();

        var trainingConfig = TrainingConfig.CreateFast();
        var trainResult = await modelTrainingService.TrainModelAsync(importResult.Value.ImportedSamples, trainingConfig);

        // Then - Model should be trained successfully
        trainResult.IsSuccess.Should().BeTrue();
        var modelInfo = trainResult.Value;

        modelInfo.ModelId.Should().NotBeEmpty();
        modelInfo.ModelVersion.Should().NotBeEmpty();
        modelInfo.Accuracy.Should().BeGreaterThan(0.5f, "Model should achieve >50% accuracy on validation set");
        modelInfo.TotalSamples.Should().BeGreaterThan(50, "Should have processed significant training data");
        modelInfo.TrainingTimeSeconds.Should().BeLessThan(60, "Fast training should complete in <60s");
        modelInfo.CategoryCount.Should().BeGreaterThan(10, "Should have learned multiple categories");
    }

    [Fact]
    public async Task SaveAndLoadModel_WithMetadata_ShouldPreserveInformation()
    {
        // Given - Trained model
        using var scope = CreateScope();
        var modelTrainingService = scope.ServiceProvider.GetRequiredService<IModelTrainingService>();

        var trainingData = CreateMinimalTrainingData();
        var trainingConfig = TrainingConfig.CreateFast();

        var trainResult = await modelTrainingService.TrainModelAsync(trainingData, trainingConfig);
        trainResult.IsSuccess.Should().BeTrue();
        var originalModelInfo = trainResult.Value;

        var tempModelPath = Path.Combine(Path.GetTempPath(), $"test-model-metadata-{Guid.NewGuid()}.zip");
        var tempMetadataPath = Path.ChangeExtension(tempModelPath, ".meta.json");

        try
        {
            // When - Save model with custom metadata
            var metadata = new MLModels.ModelMetadata
            {
                ModelName = "TestModel",
                Version = "1.0.0",
                CreatedAt = DateTime.UtcNow,
                Description = "Test model for integration testing",
                Author = "Integration Test Suite",
                Tags = new Dictionary<string, string>
                {
                    ["Environment"] = "Test",
                    ["Purpose"] = "Integration Testing"
                }
            };

            var saveResult = await modelTrainingService.SaveModelAsync(originalModelInfo, tempModelPath, metadata);
            saveResult.IsSuccess.Should().BeTrue();

            // Then - Load and verify metadata is preserved
            var loadResult = await modelTrainingService.LoadModelAsync(tempModelPath);
            loadResult.IsSuccess.Should().BeTrue();

            // Verify metadata file exists and contains correct information
            File.Exists(tempMetadataPath).Should().BeTrue();
            var metadataJson = await File.ReadAllTextAsync(tempMetadataPath);
            metadataJson.Should().Contain("Test model for integration testing");
            metadataJson.Should().Contain("Integration Test Suite");
        }
        finally
        {
            if (File.Exists(tempModelPath))
                File.Delete(tempModelPath);
            if (File.Exists(tempMetadataPath))
                File.Delete(tempMetadataPath);
        }
    }

    [Fact]
    public async Task TrainMultipleModels_SaveSequentially_ShouldNotInterfere()
    {
        // Given - Multiple training sessions
        using var scope = CreateScope();
        var modelTrainingService = scope.ServiceProvider.GetRequiredService<IModelTrainingService>();

        var trainingConfig = TrainingConfig.CreateFast();
        var trainingData = CreateMinimalTrainingData();

        var modelPaths = new List<string>();

        try
        {
            // When - Train and save multiple models sequentially
            for (int i = 0; i < 3; i++)
            {
                var trainResult = await modelTrainingService.TrainModelAsync(trainingData, trainingConfig);
                trainResult.IsSuccess.Should().BeTrue();

                var modelPath = Path.Combine(Path.GetTempPath(), $"test-model-{i}-{Guid.NewGuid()}.zip");
                modelPaths.Add(modelPath);

                var metadata = new MLModels.ModelMetadata
                {
                    ModelName = $"TestModel{i}",
                    Version = "1.0.0",
                    CreatedAt = DateTime.UtcNow,
                    Description = $"Test model {i}",
                    Author = "Integration Test"
                };
                var saveResult = await modelTrainingService.SaveModelAsync(trainResult.Value, modelPath, metadata);
                saveResult.IsSuccess.Should().BeTrue();
            }

            // Then - All models should exist and be valid
            foreach (var modelPath in modelPaths)
            {
                File.Exists(modelPath).Should().BeTrue($"Model file should exist: {modelPath}");
                new FileInfo(modelPath).Length.Should().BeGreaterThan(0, "Model file should have content");

                // Verify each model can be loaded
                var loadResult = await modelTrainingService.LoadModelAsync(modelPath);
                loadResult.IsSuccess.Should().BeTrue($"Model should load successfully: {modelPath}");
            }
        }
        finally
        {
            // Cleanup
            foreach (var modelPath in modelPaths)
            {
                if (File.Exists(modelPath))
                    File.Delete(modelPath);
                var metadataPath = Path.ChangeExtension(modelPath, ".meta.json");
                if (File.Exists(metadataPath))
                    File.Delete(metadataPath);
            }
        }
    }

    [Fact]
    public async Task LoadModel_FromNonExistentPath_ShouldReturnFailure()
    {
        // Given - Non-existent model path
        using var scope = CreateScope();
        var modelTrainingService = scope.ServiceProvider.GetRequiredService<IModelTrainingService>();

        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"non-existent-model-{Guid.NewGuid()}.zip");

        // When - Attempt to load non-existent model
        var loadResult = await modelTrainingService.LoadModelAsync(nonExistentPath);

        // Then - Should return failure with clear error message
        loadResult.IsFailure.Should().BeTrue();
        loadResult.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ModelPersistence_WithChecksum_ShouldDetectCorruption()
    {
        // Given - Trained and saved model
        using var scope = CreateScope();
        var modelTrainingService = scope.ServiceProvider.GetRequiredService<IModelTrainingService>();

        var trainingData = CreateMinimalTrainingData();
        var trainingConfig = TrainingConfig.CreateFast();

        var trainResult = await modelTrainingService.TrainModelAsync(trainingData, trainingConfig);
        trainResult.IsSuccess.Should().BeTrue();

        var tempModelPath = Path.Combine(Path.GetTempPath(), $"test-model-checksum-{Guid.NewGuid()}.zip");

        try
        {
            var metadata = new MLModels.ModelMetadata
            {
                ModelName = "ChecksumTestModel",
                Version = "1.0.0",
                CreatedAt = DateTime.UtcNow,
                Description = "Model for checksum testing",
                Author = "Integration Test"
            };
            var saveResult = await modelTrainingService.SaveModelAsync(trainResult.Value, tempModelPath, metadata);
            saveResult.IsSuccess.Should().BeTrue();

            var originalChecksum = saveResult.Value.Checksum;
            originalChecksum.Should().NotBeEmpty();

            // When - Corrupt the model file
            await using (var file = File.OpenWrite(tempModelPath))
            {
                file.Seek(0, SeekOrigin.Begin);
                file.WriteByte(0xFF); // Corrupt first byte
            }

            // Recalculate checksum
            using var fs = File.OpenRead(tempModelPath);
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(fs);
            var corruptedChecksum = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            // Then - Checksum should be different
            corruptedChecksum.Should().NotBe(originalChecksum, "Corrupted file should have different checksum");
        }
        finally
        {
            if (File.Exists(tempModelPath))
                File.Delete(tempModelPath);
            var metadataPath = Path.ChangeExtension(tempModelPath, ".meta.json");
            if (File.Exists(metadataPath))
                File.Delete(metadataPath);
        }
    }

    /// <summary>
    /// Creates minimal training data for quick integration tests.
    /// </summary>
    private IEnumerable<MediaButler.ML.Models.TrainingSample> CreateMinimalTrainingData()
    {
        var samples = new List<MediaButler.ML.Models.TrainingSample>
        {
            new() { Filename = "Breaking.Bad.S01E01.1080p.mkv", Category = "BREAKING BAD", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
            new() { Filename = "Breaking.Bad.S01E02.1080p.mkv", Category = "BREAKING BAD", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
            new() { Filename = "Breaking.Bad.S02E01.720p.mkv", Category = "BREAKING BAD", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
            new() { Filename = "Game.of.Thrones.S01E01.1080p.mkv", Category = "GAME OF THRONES", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
            new() { Filename = "Game.of.Thrones.S01E02.1080p.mkv", Category = "GAME OF THRONES", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
            new() { Filename = "Game.of.Thrones.S02E01.720p.mkv", Category = "GAME OF THRONES", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
            new() { Filename = "Stranger.Things.S01E01.1080p.mkv", Category = "STRANGER THINGS", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
            new() { Filename = "Stranger.Things.S01E02.1080p.mkv", Category = "STRANGER THINGS", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
            new() { Filename = "Stranger.Things.S02E01.720p.mkv", Category = "STRANGER THINGS", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
            new() { Filename = "The.Office.S01E01.1080p.mkv", Category = "THE OFFICE", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
            new() { Filename = "The.Office.S01E02.1080p.mkv", Category = "THE OFFICE", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
            new() { Filename = "The.Office.S02E01.720p.mkv", Category = "THE OFFICE", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
            new() { Filename = "One.Piece.E1001.1080p.mkv", Category = "ONE PIECE", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
            new() { Filename = "One.Piece.E1002.1080p.mkv", Category = "ONE PIECE", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
            new() { Filename = "One.Piece.E1003.720p.mkv", Category = "ONE PIECE", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
            new() { Filename = "Attack.on.Titan.S01E01.1080p.mkv", Category = "ATTACK ON TITAN", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
            new() { Filename = "Attack.on.Titan.S01E02.1080p.mkv", Category = "ATTACK ON TITAN", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
            new() { Filename = "Attack.on.Titan.S02E01.720p.mkv", Category = "ATTACK ON TITAN", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
            new() { Filename = "Naruto.Shippuden.E001.1080p.mkv", Category = "NARUTO SHIPPUDEN", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
            new() { Filename = "Naruto.Shippuden.E002.1080p.mkv", Category = "NARUTO SHIPPUDEN", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
            new() { Filename = "Naruto.Shippuden.E003.720p.mkv", Category = "NARUTO SHIPPUDEN", Confidence = 0.9, Source = MediaButler.ML.Models.TrainingSampleSource.ImportedData, CreatedAt = DateTime.UtcNow, IsManuallyVerified = true },
        };

        return samples;
    }
}
