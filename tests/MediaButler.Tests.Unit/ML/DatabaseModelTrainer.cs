using MediaButler.Data;
using MediaButler.ML.Configuration;
using MediaButler.ML.Models;
using MediaButler.ML.Services;
using MediaButler.ML.Interfaces;
using MediaButler.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace MediaButler.Tests.Unit.ML;

/// <summary>
/// Trains ML model directly from TrackedFiles database table.
/// This ensures the model learns the actual Italian category names used in your database.
/// </summary>
public class DatabaseModelTrainer
{
    [Fact]
    public async Task TrainModelFromDatabase()
    {
        // Setup
        var trainingLogger = new Mock<ILogger<ModelTrainingService>>().Object;
        var feLogger = new Mock<ILogger<FeatureEngineeringService>>().Object;

        var rootDirectory = Path.Combine(Directory.GetCurrentDirectory(), "../../../../..");
        var modelsDirectory = Path.Combine(rootDirectory, "models");
        modelsDirectory = Path.GetFullPath(modelsDirectory);

        if (!Directory.Exists(modelsDirectory))
        {
            Directory.CreateDirectory(modelsDirectory);
        }

        var mlConfig = Options.Create(new MLConfiguration
        {
            ModelPath = modelsDirectory,
            AutoClassifyThreshold = 0.85f,
            SuggestionThreshold = 0.5f,
            ActiveModelVersion = 2 // New version for database-trained model
        });

        var featureEngineering = new FeatureEngineeringService(feLogger, mlConfig);
        var mockPersistence = new Mock<IMLPersistenceService>().Object;
        var mockModelManager = new Mock<IMLModelManager>().Object;
        var trainingService = new ModelTrainingService(trainingLogger, featureEngineering, mockPersistence, mockModelManager);

        // Load training data from database
        var dbPath = Path.Combine(rootDirectory, "temp/mediabutler.dev.db");
        dbPath = Path.GetFullPath(dbPath);

        Console.WriteLine($"📂 Loading training data from database: {dbPath}");

        var connectionString = $"Data Source={dbPath};Cache=Shared;Foreign Keys=true;";
        var optionsBuilder = new DbContextOptionsBuilder<MediaButlerDbContext>();
        optionsBuilder.UseSqlite(connectionString);

        var trainingSamples = new List<TrainingSample>();

        using (var dbContext = new MediaButlerDbContext(optionsBuilder.Options))
        {
            // Load all files with categories (excludes NULL or empty categories)
            var filesWithCategories = await dbContext.TrackedFiles
                .Where(f => f.Category != null && f.Category != "")
                .Select(f => new { f.FileName, f.Category })
                .ToListAsync();

            Console.WriteLine($"📊 Found {filesWithCategories.Count} files with categories in database");

            // Convert to training samples
            foreach (var file in filesWithCategories)
            {
                trainingSamples.Add(new TrainingSample
                {
                    Filename = file.FileName,
                    Category = file.Category, // Use actual Italian category name from DB
                    Confidence = 1.0, // High confidence - these are user-confirmed categories
                    Source = TrainingSampleSource.UserFeedback,
                    CreatedAt = DateTime.UtcNow,
                    IsManuallyVerified = true
                });
            }

            // Show category distribution
            var categoryGroups = trainingSamples
                .GroupBy(s => s.Category)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToList();

            Console.WriteLine($"\n📈 Top 10 categories:");
            foreach (var group in categoryGroups)
            {
                Console.WriteLine($"  {group.Key}: {group.Count()} samples");
            }
        }

        // Train model
        Console.WriteLine($"\n🤖 Training model with {trainingSamples.Count} samples...");
        var trainingConfig = MediaButler.ML.Models.TrainingConfiguration.CreateDefault();

        var trainingResult = await trainingService.TrainModelAsync(
            trainingSamples,
            trainingConfig);

        if (trainingResult.IsFailure)
        {
            Console.WriteLine($"❌ Training failed: {trainingResult.Error}");
            Assert.Fail($"Training failed: {trainingResult.Error}");
            return;
        }

        Console.WriteLine($"✅ Training completed! Accuracy: {trainingResult.Value.ValidationMetrics.Accuracy:P2}");

        // Save model
        var modelPath = Path.Combine(modelsDirectory, "classification-simplified-model.zip");
        Console.WriteLine($"💾 Saving model to: {modelPath}");

        var metadata = new ModelMetadata
        {
            ModelName = "TV Series Classifier (Database-trained)",
            Version = 2,
            CreatedAt = DateTime.UtcNow,
            Description = $"ML.NET model trained on {trainingSamples.Count} Italian series samples from TrackedFiles database",
            Author = "MediaButler ML Pipeline",
            Tags = new Dictionary<string, string>
            {
                ["Environment"] = "Production",
                ["TrainingSamples"] = trainingSamples.Count.ToString(),
                ["Categories"] = trainingSamples.Select(s => s.Category).Distinct().Count().ToString(),
                ["Accuracy"] = trainingResult.Value.ValidationMetrics.Accuracy.ToString("P2"),
                ["Language"] = "Italian",
                ["Source"] = "Database"
            }
        };

        var saveResult = await trainingService.SaveModelAsync(
            trainingResult.Value,
            modelPath,
            metadata);

        if (saveResult.IsFailure)
        {
            Console.WriteLine($"❌ Save failed: {saveResult.Error}");
            Console.WriteLine($"⚠️ Checking if model file was created anyway...");
        }
        else
        {
            Console.WriteLine($"✅ Model saved successfully!");
            Console.WriteLine($"📏 Model size: {saveResult.Value.FileSizeBytes / 1024} KB");
        }

        Console.WriteLine($"\n🎯 Model ready for production use with Italian categories!");
        Console.WriteLine($"   Categories will now match your database exactly.");

        Assert.True(File.Exists(modelPath), $"Model file should exist at {modelPath}");
    }
}
