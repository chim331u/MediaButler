using MediaButler.Core.Common;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MediaButler.ML.Services;

/// <summary>
/// Simple implementation of ML-powered file classification service.
/// This is a temporary implementation that provides mock responses for integration tests.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Provides classification interface
/// - No complecting: Independent mock implementation
/// - Values over state: Stateless mock operations
/// - TODO: Replace with full implementation orchestrating existing services
/// </remarks>
public class ClassificationService : IClassificationService
{
    private readonly ILogger<ClassificationService> _logger;

    public ClassificationService(ILogger<ClassificationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Mock classification for integration tests.
    /// Returns a basic classification result for any filename.
    /// </summary>
    public async Task<Result<ClassificationResult>> ClassifyFilenameAsync(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return Result<ClassificationResult>.Failure("Filename cannot be null or empty");
        }

        _logger.LogDebug("Mock classification for filename: {Filename}", filename);

        await Task.Delay(10); // Simulate async work

        // Mock classification result
        var result = new ClassificationResult
        {
            Filename = filename,
            PredictedCategory = "MOCK SERIES",
            Confidence = 0.75f,
            AlternativePredictions = new[]
            {
                new CategoryPrediction { Category = "ALTERNATIVE 1", Confidence = 0.65f },
                new CategoryPrediction { Category = "ALTERNATIVE 2", Confidence = 0.55f }
            },
            Decision = ClassificationDecision.SuggestWithAlternatives,
            Features = new Dictionary<string, object>
            {
                ["TokenCount"] = 4,
                ["HasSeasonEpisode"] = true,
                ["Quality"] = "1080p"
            },
            ModelVersion = "mock-1.0.0",
            ClassifiedAt = DateTime.UtcNow,
            ProcessingTimeMs = 10
        };

        return Result<ClassificationResult>.Success(result);
    }

    /// <summary>
    /// Mock batch classification for integration tests.
    /// </summary>
    public async Task<Result<IEnumerable<ClassificationResult>>> ClassifyBatchAsync(IEnumerable<string> filenames)
    {
        if (filenames == null)
        {
            return Result<IEnumerable<ClassificationResult>>.Failure("Filenames collection cannot be null");
        }

        var results = new List<ClassificationResult>();

        foreach (var filename in filenames)
        {
            var result = await ClassifyFilenameAsync(filename);
            if (result.IsSuccess)
            {
                results.Add(result.Value);
            }
        }

        return Result<IEnumerable<ClassificationResult>>.Success(results);
    }

    /// <summary>
    /// Returns mock categories for integration tests.
    /// </summary>
    public Result<IEnumerable<string>> GetAvailableCategories()
    {
        var categories = new[] { "MOCK SERIES", "ALTERNATIVE 1", "ALTERNATIVE 2", "TEST CATEGORY" };
        return Result<IEnumerable<string>>.Success(categories);
    }

    /// <summary>
    /// Returns mock model information.
    /// </summary>
    public Result<ModelInfo> GetModelInfo()
    {
        var modelInfo = new ModelInfo
        {
            Version = "mock-1.0.0",
            Algorithm = "Mock Classification Model",
            TrainedAt = DateTime.UtcNow.AddDays(-30),
            TestAccuracy = 0.85f,
            CategoryCount = 4,
            TrainingSamples = 1000,
            TestPrecision = 0.80f,
            TestRecall = 0.82f,
            TestF1Score = 0.81f,
            FileSizeBytes = 20 * 1024 * 1024, // 20MB
            AverageInferenceTimeMs = 50.0
        };

        return Result<ModelInfo>.Success(modelInfo);
    }

    /// <summary>
    /// Mock model ready check - always returns true for testing.
    /// </summary>
    public bool IsModelReady()
    {
        return true;
    }
}