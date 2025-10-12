using MediaButler.ML.Configuration;
using MediaButler.ML.Services;
using MediaButler.ML.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TrainingModels = MediaButler.ML.Models;

namespace MediaButler.Tests.Unit.ML;

/// <summary>
/// Production model training script.
/// Trains the ML model with real CSV data and saves it to the models directory.
/// </summary>
public class ProductionModelTrainer
{
    [Fact]
    public async Task TrainProductionModel_SaveToModelsDirectory()
    {
        // Arrange - Setup services with PRODUCTION models directory
        var importerLogger = new Mock<ILogger<CsvTrainingDataImporter>>().Object;
        var trainingLogger = new Mock<ILogger<ModelTrainingService>>().Object;
        var feLogger = new Mock<ILogger<FeatureEngineeringService>>().Object;

        // Use the actual production models directory
        var rootDirectory = Path.Combine(Directory.GetCurrentDirectory(), "../../../../..");
        var modelsDirectory = Path.Combine(rootDirectory, "models");
        modelsDirectory = Path.GetFullPath(modelsDirectory);

        // Ensure models directory exists
        if (!Directory.Exists(modelsDirectory))
        {
            Directory.CreateDirectory(modelsDirectory);
        }

        var mlConfig = Options.Create(new MLConfiguration
        {
            ModelPath = modelsDirectory,
            AutoClassifyThreshold = 0.85f,
            SuggestionThreshold = 0.5f,
            ActiveModelVersion = "1.0.0"
        });

        var featureEngineering = new FeatureEngineeringService(feLogger, mlConfig);
        var csvImporter = new CsvTrainingDataImporter(importerLogger);
        var trainingService = new ModelTrainingService(trainingLogger, featureEngineering);

        // Act - Import real CSV data
        var realCsvPath = Path.Combine(rootDirectory, "data/training/tv-series-training-data.csv");
        realCsvPath = Path.GetFullPath(realCsvPath);

        Console.WriteLine($"\n📂 === PRODUCTION MODEL TRAINING ===");
        Console.WriteLine($"📂 Loading training data from: {realCsvPath}");
        Console.WriteLine($"📂 Current Directory: {Directory.GetCurrentDirectory()}");
        Console.WriteLine($"📂 Models Directory: {modelsDirectory}");
        Console.WriteLine($"📂 CSV File exists: {File.Exists(realCsvPath)}");

        if (!File.Exists(realCsvPath))
        {
            Console.WriteLine("❌ CSV file not found - cannot train production model");
            Assert.Fail("Training data CSV not found");
            return;
        }

        var config = new TrainingModels.CsvImportConfiguration
        {
            HasHeader = true,
            NormalizeCategoryNames = true,
            SkipDuplicates = true,
            Separator = ';'
        };

        var importResult = await csvImporter.ImportFromCsvAsync(realCsvPath, config);

        // Assert - Validate import
        Console.WriteLine($"\n📊 CSV Import Results:");
        Console.WriteLine($"  ✅ Success: {importResult.IsSuccess}");
        Console.WriteLine($"  📄 Total Rows: {importResult.Value.TotalRows}");
        Console.WriteLine($"  ✓ Valid Rows: {importResult.Value.ValidRows}");
        Console.WriteLine($"  ⏭ Skipped Rows: {importResult.Value.SkippedRows}");
        Console.WriteLine($"  🔁 Duplicate Rows: {importResult.Value.DuplicateRows}");
        Console.WriteLine($"  📁 Categories: {importResult.Value.Categories.Count}");
        Console.WriteLine($"  ⏱ Processing Time: {importResult.Value.ProcessingTime.TotalMilliseconds:F2}ms");

        Console.WriteLine($"\n📂 Categories Found:");
        foreach (var category in importResult.Value.Categories.OrderBy(c => c))
        {
            var count = importResult.Value.ImportedSamples.Count(s => s.Category == category);
            Console.WriteLine($"  - {category}: {count} samples");
        }

        Assert.True(importResult.IsSuccess);
        Assert.True(importResult.Value.ValidRows >= 100, $"Expected at least 100 valid rows, got {importResult.Value.ValidRows}");
        Assert.True(importResult.Value.Categories.Count >= 15, $"Expected at least 15 categories, got {importResult.Value.Categories.Count}");

        // Act - Train model with production-quality configuration
        Console.WriteLine($"\n🤖 Training Production ML Model with {importResult.Value.ValidRows} samples...");
        Console.WriteLine($"📁 Model will be saved to: {Path.Combine(modelsDirectory, "classification-model.zip")}");

        var trainingConfig = TrainingModels.TrainingConfiguration.CreateDefault(); // Use DEFAULT for production quality

        var startTime = DateTime.UtcNow;
        var trainingResult = await trainingService.TrainModelAsync(
            importResult.Value.ImportedSamples,
            trainingConfig);
        var trainingDuration = DateTime.UtcNow - startTime;

        // Assert - Validate training results
        Console.WriteLine($"\n🎯 Training Results:");
        Console.WriteLine($"  ✅ Success: {trainingResult.IsSuccess}");

        if (trainingResult.IsFailure)
        {
            Console.WriteLine($"  ❌ Error: {trainingResult.Error}");
            Assert.Fail($"Training failed: {trainingResult.Error}");
            return;
        }

        var model = trainingResult.Value;

        Console.WriteLine($"  📊 Training Samples: {model.TrainingSampleCount}");
        Console.WriteLine($"  ⏱ Training Duration: {model.TrainingDuration.TotalSeconds:F2}s");
        Console.WriteLine($"  💾 Model Path: {model.ModelPath}");
        Console.WriteLine($"  🆔 Model Version: {model.ModelVersion}");
        Console.WriteLine($"  📏 Model File Size: {new FileInfo(model.ModelPath).Length / 1024} KB");

        Console.WriteLine($"\n📈 Validation Metrics:");
        Console.WriteLine($"  🎯 Accuracy: {model.ValidationMetrics.Accuracy:P2}");
        Console.WriteLine($"  📊 Macro F1 Score: {model.ValidationMetrics.MacroF1Score:P2}");
        Console.WriteLine($"  📊 Weighted F1 Score: {model.ValidationMetrics.WeightedF1Score:P2}");
        Console.WriteLine($"  ⚖️ Log Loss: {model.ValidationMetrics.LogLoss:F4}");
        Console.WriteLine($"  📍 Macro Precision: {model.ValidationMetrics.MacroPrecision:P2}");
        Console.WriteLine($"  📍 Macro Recall: {model.ValidationMetrics.MacroRecall:P2}");

        Console.WriteLine($"\n🎯 Production Quality Requirements:");
        Console.WriteLine($"  Target Accuracy: >80%");
        Console.WriteLine($"  Actual Accuracy: {model.ValidationMetrics.Accuracy:P2} {(model.ValidationMetrics.Accuracy > 0.8 ? "✅" : "❌")}");
        Console.WriteLine($"  Target F1 Score: >70%");
        Console.WriteLine($"  Actual F1 Score: {model.ValidationMetrics.MacroF1Score:P2} {(model.ValidationMetrics.MacroF1Score > 0.7 ? "✅" : "❌")}");
        Console.WriteLine($"  Target Training Time: <10 minutes");
        Console.WriteLine($"  Actual Training Time: {trainingDuration.TotalSeconds:F2}s {(trainingDuration.TotalMinutes < 10 ? "✅" : "❌")}");

        // Production Quality Gates
        Assert.True(trainingResult.IsSuccess, "Training should succeed");
        Assert.True(model.TrainingSampleCount >= 100, $"Should have at least 100 training samples, got {model.TrainingSampleCount}");
        Assert.True(model.ValidationMetrics.Accuracy > 0.8,
            $"Production target: >80% accuracy, got {model.ValidationMetrics.Accuracy:P2}");
        Assert.True(model.ValidationMetrics.MacroF1Score > 0.7,
            $"Production target: >70% F1 score, got {model.ValidationMetrics.MacroF1Score:P2}");
        Assert.True(trainingDuration.TotalMinutes < 10,
            $"Training should complete in <10 minutes, took {trainingDuration.TotalMinutes:F2} minutes");
        Assert.True(File.Exists(model.ModelPath), "Model file should exist at specified path");

        // Verify model is in the correct location
        var expectedModelPath = Path.Combine(modelsDirectory, "classification-model.zip");
        Assert.True(File.Exists(expectedModelPath), $"Model should exist at {expectedModelPath}");

        Console.WriteLine($"\n✅ Production Model Training Complete!");
        Console.WriteLine($"📁 Model saved to: {expectedModelPath}");
        Console.WriteLine($"🚀 Ready for production use!");
    }
}
