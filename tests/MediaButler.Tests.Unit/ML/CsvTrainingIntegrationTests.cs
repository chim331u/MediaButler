using FluentAssertions;
using MediaButler.ML.Configuration;
using MediaButler.ML.Interfaces;
using MediaButler.Core.Interfaces;
using MediaButler.ML.Services;
using MediaButler.ML.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TrainingModels = MediaButler.ML.Models;

namespace MediaButler.Tests.Unit.ML;

/// <summary>
/// Integration tests for training ML models from CSV data.
/// Tests the complete pipeline: CSV import → Model Training → Evaluation
/// </summary>
public class CsvTrainingIntegrationTests : IDisposable
{
    private readonly string _testDataDirectory;
    private readonly string _testModelsDirectory;
    private readonly CsvTrainingDataImporter _csvImporter;
    private readonly ModelTrainingService _trainingService;
    private readonly ILogger<CsvTrainingDataImporter> _importerLogger;
    private readonly ILogger<ModelTrainingService> _trainingLogger;
    private readonly IFeatureEngineeringService _featureEngineering;

    public CsvTrainingIntegrationTests()
    {
        // Setup test directories
        _testDataDirectory = Path.Combine(Path.GetTempPath(), "mediabutler-tests", Guid.NewGuid().ToString());
        _testModelsDirectory = Path.Combine(_testDataDirectory, "models");
        Directory.CreateDirectory(_testDataDirectory);
        Directory.CreateDirectory(_testModelsDirectory);

        // Setup loggers
        _importerLogger = new Mock<ILogger<CsvTrainingDataImporter>>().Object;
        _trainingLogger = new Mock<ILogger<ModelTrainingService>>().Object;

        // Create feature engineering service (required dependency)
        var feLogger = new Mock<ILogger<FeatureEngineeringService>>().Object;
        var mlConfig = Options.Create(new MLConfiguration
        {
            ModelPath = _testModelsDirectory,
            AutoClassifyThreshold = 0.85f,
            SuggestionThreshold = 0.5f
        });
        _featureEngineering = new FeatureEngineeringService(feLogger, mlConfig);

        // Create services
        _csvImporter = new CsvTrainingDataImporter(_importerLogger);
        var mockPersistence = new Mock<IMLPersistenceService>().Object;
        var mockModelManager = new Mock<IMLModelManager>().Object;
        _trainingService = new ModelTrainingService(_trainingLogger, _featureEngineering, mockPersistence, mockModelManager);
    }

    [Fact]
    public async Task ImportCsvData_WithValidFile_ImportsAllSamples()
    {
        // Arrange
        var csvPath = Path.Combine(_testDataDirectory, "test-training.csv");
        var csvContent = @"Filename;Category
Breaking.Bad.S05E16.FINAL.ITA.1080p.BluRay.x264-KILLERS.mkv;BREAKING BAD
Game.of.Thrones.S08E06.FINAL.ITA.1080p.WEB-DLMux.x264-NovaRip.mkv;GAME OF THRONES
One.Piece.1089.Sub.ITA.1080p.WEB-DLMux.x264-UBi.mkv;ONE PIECE
Stranger.Things.4x09.FINAL.ITA.ENG.1080p.NF.WEB-DLMux-DarkSideMux.mkv;STRANGER THINGS
The.Walking.Dead.11x24.FINAL.ITA.1080p.WEB-DLMux.x264-NovaRip.mkv;THE WALKING DEAD";

        await File.WriteAllTextAsync(csvPath, csvContent);

        var config = new TrainingModels.CsvImportConfiguration
        {
            HasHeader = true,
            Separator = ';',
            NormalizeCategoryNames = true,
            SkipDuplicates = true
        };

        // Act
        var result = await _csvImporter.ImportFromCsvAsync(csvPath, config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ValidRows.Should().Be(5);
        result.Value.SkippedRows.Should().Be(0);
        result.Value.DuplicateRows.Should().Be(0);
        result.Value.Categories.Should().HaveCount(5);
        result.Value.ImportedSamples.Should().HaveCount(5);
        result.Value.IsSuccessful.Should().BeTrue();
        result.Value.SuccessRate.Should().Be(100.0);
    }

    [Fact]
    public async Task ImportCsvData_WithDuplicates_SkipsDuplicateFilenames()
    {
        // Arrange
        var csvPath = Path.Combine(_testDataDirectory, "test-duplicates.csv");
        var csvContent = @"Filename;Category
Breaking.Bad.S05E16.mkv;BREAKING BAD
Breaking.Bad.S05E16.mkv;BREAKING BAD
Game.of.Thrones.S08E06.mkv;GAME OF THRONES";

        await File.WriteAllTextAsync(csvPath, csvContent);

        var config = new TrainingModels.CsvImportConfiguration
        {
            HasHeader = false,
            SkipDuplicates = true
        };

        // Act
        var result = await _csvImporter.ImportFromCsvAsync(csvPath, config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ValidRows.Should().Be(2);
        result.Value.DuplicateRows.Should().Be(1);
        result.Value.ImportedSamples.Should().HaveCount(2);
    }

    [Fact]
    public async Task ImportCsvData_WithInvalidExtensions_SkipsInvalidFiles()
    {
        // Arrange
        var csvPath = Path.Combine(_testDataDirectory, "test-extensions.csv");
        var csvContent = @"Filename;Category
Breaking.Bad.S05E16.mkv;BREAKING BAD
Some.Document.pdf;DOCUMENTS
Game.of.Thrones.S08E06.avi;GAME OF THRONES";

        await File.WriteAllTextAsync(csvPath, csvContent);

        var config = new TrainingModels.CsvImportConfiguration
        {
            HasHeader = true,
            ValidateFileExtensions = true
        };

        // Act
        var result = await _csvImporter.ImportFromCsvAsync(csvPath, config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ValidRows.Should().Be(2); // mkv and avi are valid
        result.Value.SkippedRows.Should().Be(1); // pdf is skipped
        result.Value.Errors.Should().HaveCount(1);
        result.Value.Errors[0].Should().Contain(".pdf");
    }

    [Fact]
    public async Task ImportCsvData_WithMaxRows_LimitsImport()
    {
        // Arrange
        var csvPath = Path.Combine(_testDataDirectory, "test-maxrows.csv");
        var csvContent = @"Filename;Category
File1.mkv;SERIES1
File2.mkv;SERIES2
File3.mkv;SERIES3
File4.mkv;SERIES4
File5.mkv;SERIES5";

        await File.WriteAllTextAsync(csvPath, csvContent);

        var config = new TrainingModels.CsvImportConfiguration
        {
            HasHeader = false,
            MaxRows = 3
        };

        // Act
        var result = await _csvImporter.ImportFromCsvAsync(csvPath, config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ValidRows.Should().Be(3);
        result.Value.ImportedSamples.Should().HaveCount(3);
    }

    [Fact]
    public async Task ValidateCsvFormat_WithValidFile_ReturnsSuccess()
    {
        // Arrange
        var csvPath = Path.Combine(_testDataDirectory, "test-validate.csv");
        var csvContent = "Filename;Category\nBreaking.Bad.S05E16.mkv;BREAKING BAD";
        await File.WriteAllTextAsync(csvPath, csvContent);

        // Act
        var result = await _csvImporter.ValidateCsvFormatAsync(csvPath);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateCsvFormat_WithInvalidSeparator_ReturnsFailure()
    {
        // Arrange
        var csvPath = Path.Combine(_testDataDirectory, "test-invalid.csv");
        var csvContent = "Filename,Category\nBreaking.Bad.S05E16.mkv,BREAKING BAD"; // Using comma instead of semicolon
        await File.WriteAllTextAsync(csvPath, csvContent);

        // Act
        var result = await _csvImporter.ValidateCsvFormatAsync(csvPath); // Default expects semicolon

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("expected at least 2 columns");
    }

    [Fact]
    public async Task GetCsvPreview_ReturnsFirstRows()
    {
        // Arrange
        var csvPath = Path.Combine(_testDataDirectory, "test-preview.csv");
        var csvContent = @"Filename;Category
File1.mkv;SERIES1
File2.mkv;SERIES2
File3.mkv;SERIES3
File4.mkv;SERIES4
File5.mkv;SERIES5";

        await File.WriteAllTextAsync(csvPath, csvContent);

        var config = new TrainingModels.CsvImportConfiguration { HasHeader = true };

        // Act
        var result = await _csvImporter.GetCsvPreviewAsync(csvPath, previewRows: 3, config: config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value[0].Filename.Should().Be("File1.mkv");
        result.Value[0].Category.Should().Be("SERIES1");
    }

    [Fact]
    public async Task TrainModel_WithImportedCsvData_TrainsSuccessfully()
    {
        // Arrange - Create larger training dataset
        var csvPath = Path.Combine(_testDataDirectory, "training-data.csv");
        var csvContent = GenerateLargeTrainingDataset(50); // 50 samples across 5 categories
        await File.WriteAllTextAsync(csvPath, csvContent);

        var config = new TrainingModels.CsvImportConfiguration
        {
            HasHeader = true,
            NormalizeCategoryNames = true
        };

        // Import CSV data
        var importResult = await _csvImporter.ImportFromCsvAsync(csvPath, config);
        importResult.IsSuccess.Should().BeTrue();
        importResult.Value.ImportedSamples.Should().HaveCount(50);

        // Arrange training configuration
        var trainingConfig = TrainingModels.TrainingConfiguration.CreateFast(); // Use fast config for testing

        // Act - Train model
        var trainingResult = await _trainingService.TrainModelAsync(
            importResult.Value.ImportedSamples,
            trainingConfig);

        // Assert
        trainingResult.IsSuccess.Should().BeTrue();
        trainingResult.Value.Should().NotBeNull();
        trainingResult.Value.TrainingSampleCount.Should().Be(50);
        trainingResult.Value.ValidationMetrics.Should().NotBeNull();
        trainingResult.Value.ValidationMetrics.Accuracy.Should().BeGreaterThan(0.5); // Expect reasonable accuracy
        trainingResult.Value.TrainingDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task TrainModel_WithRealCsvFile_LoadsAndTrains()
    {
        // Arrange - Use the actual training data CSV if it exists
        var realCsvPath = Path.GetFullPath("../../../../../../../data/training/tv-series-training-data.csv");

        if (!File.Exists(realCsvPath))
        {
            // Skip test if real CSV doesn't exist
            return;
        }

        var config = new TrainingModels.CsvImportConfiguration
        {
            HasHeader = true,
            NormalizeCategoryNames = true,
            SkipDuplicates = true
        };

        // Import real training data
        var importResult = await _csvImporter.ImportFromCsvAsync(realCsvPath, config);

        // Assert import
        importResult.IsSuccess.Should().BeTrue();
        importResult.Value.ImportedSamples.Should().NotBeEmpty();
        importResult.Value.Categories.Should().NotBeEmpty();

        // Log import results
        Console.WriteLine($"Imported {importResult.Value.ValidRows} samples");
        Console.WriteLine($"Categories: {string.Join(", ", importResult.Value.Categories)}");

        // Arrange training
        var trainingConfig = TrainingModels.TrainingConfiguration.CreateFast();

        // Act - Train model with real data
        var trainingResult = await _trainingService.TrainModelAsync(
            importResult.Value.ImportedSamples,
            trainingConfig);

        // Assert
        trainingResult.IsSuccess.Should().BeTrue();
        trainingResult.Value.ValidationMetrics.Accuracy.Should().BeGreaterThan(0.6); // Expect good accuracy with real data
        trainingResult.Value.TrainingSampleCount.Should().Be(importResult.Value.ValidRows);

        Console.WriteLine($"Training completed:");
        Console.WriteLine($"  Accuracy: {trainingResult.Value.ValidationMetrics.Accuracy:P2}");
        Console.WriteLine($"  F1 Score: {trainingResult.Value.ValidationMetrics.MacroF1Score:P2}");
        Console.WriteLine($"  Duration: {trainingResult.Value.TrainingDuration}");
    }

    /// <summary>
    /// Helper method to generate a larger training dataset for testing.
    /// </summary>
    private string GenerateLargeTrainingDataset(int samplesPerCategory)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Filename;Category");

        var categories = new[]
        {
            "BREAKING BAD",
            "GAME OF THRONES",
            "ONE PIECE",
            "STRANGER THINGS",
            "THE WALKING DEAD"
        };

        var qualities = new[] { "1080p", "720p", "480p" };
        var sources = new[] { "BluRay", "WEB-DLMux", "HDTV" };
        var languages = new[] { "ITA", "ENG", "ITA.ENG" };

        foreach (var category in categories)
        {
            var seriesName = category.Replace(" ", ".");

            for (int i = 1; i <= samplesPerCategory / categories.Length; i++)
            {
                var season = (i % 5) + 1;
                var episode = i;
                var quality = qualities[i % qualities.Length];
                var source = sources[i % sources.Length];
                var language = languages[i % languages.Length];

                var filename = $"{seriesName}.S{season:D2}E{episode:D2}.{quality}.{source}.{language}.mkv";
                sb.AppendLine($"{filename};{category}");
            }
        }

        return sb.ToString();
    }

    public void Dispose()
    {
        // Cleanup test directories
        if (Directory.Exists(_testDataDirectory))
        {
            try
            {
                Directory.Delete(_testDataDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }
}
