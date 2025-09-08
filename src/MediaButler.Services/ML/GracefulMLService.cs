using Microsoft.Extensions.Logging;
using MediaButler.Core.Common;
using MediaButler.Core.Models;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;

namespace MediaButler.Services.ML;

/// <summary>
/// Wrapper service that provides graceful degradation when ML services are unavailable.
/// Implements "Simple Made Easy" principles for fault tolerance.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Only handles ML service resilience
/// - Values over state: Clear failure modes and fallback values
/// - Compose don't complect: Independent fallback without complex state management
/// - Declarative: Clear policies for degradation behavior
/// </remarks>
public class GracefulMLService
{
    private readonly IPredictionService? _predictionService;
    private readonly ICategoryService? _categoryService;
    private readonly ILogger<GracefulMLService> _logger;

    /// <summary>
    /// Initializes a new instance of the GracefulMLService.
    /// </summary>
    /// <param name="predictionService">Optional prediction service</param>
    /// <param name="categoryService">Optional category service</param>
    /// <param name="logger">Logger for tracking degradation events</param>
    public GracefulMLService(
        IPredictionService? predictionService,
        ICategoryService? categoryService,
        ILogger<GracefulMLService> logger)
    {
        _predictionService = predictionService;
        _categoryService = categoryService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a value indicating whether ML services are available.
    /// </summary>
    public bool IsMLAvailable => _predictionService != null && _categoryService != null;

    /// <summary>
    /// Attempts to classify a filename with graceful fallback.
    /// </summary>
    /// <param name="filename">Filename to classify</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Classification result or fallback suggestion</returns>
    public async Task<Result<FileClassificationSuggestion>> ClassifyFileAsync(
        string filename, 
        CancellationToken cancellationToken = default)
    {
        if (!IsMLAvailable)
        {
            _logger.LogWarning("ML services unavailable, providing manual classification fallback for {Filename}", filename);
            
            return Result<FileClassificationSuggestion>.Success(new FileClassificationSuggestion
            {
                Filename = filename,
                SuggestedCategory = null,
                Confidence = 0.0f,
                AlternativeCategories = new List<CategorySuggestion>(),
                RequiresManualReview = true,
                FallbackReason = "ML services unavailable",
                ProcessingTimeMs = 0
            });
        }

        try
        {
            var predictionResult = await _predictionService!.PredictAsync(filename, cancellationToken);
            
            if (!predictionResult.IsSuccess)
            {
                _logger.LogWarning("ML prediction failed for {Filename}: {Error}", filename, predictionResult.Error);
                
                return Result<FileClassificationSuggestion>.Success(new FileClassificationSuggestion
                {
                    Filename = filename,
                    SuggestedCategory = null,
                    Confidence = 0.0f,
                    AlternativeCategories = new List<CategorySuggestion>(),
                    RequiresManualReview = true,
                    FallbackReason = $"Prediction failed: {predictionResult.Error}",
                    ProcessingTimeMs = 0
                });
            }

            // Convert ML result to user-friendly suggestion
            var suggestion = new FileClassificationSuggestion
            {
                Filename = filename,
                SuggestedCategory = predictionResult.Value.PredictedCategory,
                Confidence = predictionResult.Value.Confidence,
                AlternativeCategories = predictionResult.Value.AlternativePredictions?
                    .Select(alt => new CategorySuggestion 
                    { 
                        Category = alt.Category, 
                        Confidence = alt.Confidence 
                    }).ToList() ?? new List<CategorySuggestion>(),
                RequiresManualReview = predictionResult.Value.Confidence < 0.85f,
                FallbackReason = null,
                ProcessingTimeMs = predictionResult.Value.ProcessingTimeMs
            };

            _logger.LogDebug("ML classification completed for {Filename} with confidence {Confidence:P2}", 
                filename, predictionResult.Value.Confidence);

            return Result<FileClassificationSuggestion>.Success(suggestion);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ML classification cancelled for {Filename}", filename);
            return Result<FileClassificationSuggestion>.Failure("Classification was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during ML classification for {Filename}", filename);
            
            return Result<FileClassificationSuggestion>.Success(new FileClassificationSuggestion
            {
                Filename = filename,
                SuggestedCategory = null,
                Confidence = 0.0f,
                AlternativeCategories = new List<CategorySuggestion>(),
                RequiresManualReview = true,
                FallbackReason = $"Classification error: {ex.Message}",
                ProcessingTimeMs = 0
            });
        }
    }

    /// <summary>
    /// Gets available categories with graceful fallback.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Category list or common fallback categories</returns>
    public async Task<Result<IReadOnlyList<string>>> GetAvailableCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsMLAvailable)
        {
            _logger.LogInformation("ML services unavailable, providing default category list");
            
            // Return common Italian TV series categories as fallback
            var defaultCategories = new List<string>
            {
                "UNKNOWN",
                "TV_SERIES", 
                "MOVIES",
                "DOCUMENTARIES",
                "ANIME"
            };

            return Result<IReadOnlyList<string>>.Success(defaultCategories.AsReadOnly());
        }

        try
        {
            var registryResult = await _categoryService!.GetCategoryRegistryAsync();
            
            if (!registryResult.IsSuccess)
            {
                _logger.LogWarning("Failed to get category registry: {Error}", registryResult.Error);
                return await GetAvailableCategoriesAsync(cancellationToken); // Fallback to default
            }

            var categories = registryResult.Value.RegisteredCategories.Keys.ToList();
            return Result<IReadOnlyList<string>>.Success(categories.AsReadOnly());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving categories from ML services");
            return await GetAvailableCategoriesAsync(cancellationToken); // Fallback to default
        }
    }

    /// <summary>
    /// Records user feedback with graceful handling.
    /// </summary>
    /// <param name="filename">Original filename</param>
    /// <param name="actualCategory">User-confirmed category</param>
    /// <param name="originalPrediction">Original ML prediction</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success result or graceful failure</returns>
    public async Task<Result<Unit>> RecordFeedbackAsync(
        string filename,
        string actualCategory,
        string? originalPrediction,
        CancellationToken cancellationToken = default)
    {
        if (!IsMLAvailable)
        {
            _logger.LogInformation("ML services unavailable, feedback for {Filename} will not be recorded for model training", filename);
            return Result<Unit>.Success(Unit.Value); // Gracefully ignore
        }

        try
        {
            var feedback = new CategoryFeedback
            {
                Filename = filename,
                ActualCategory = actualCategory,
                PredictedCategory = originalPrediction,
                FeedbackType = originalPrediction == actualCategory ? FeedbackType.Correct : FeedbackType.Incorrect,
                Confidence = 1.0f, // User confirmation is 100% confident
                Timestamp = DateTime.UtcNow
            };

            await _categoryService!.RecordUserFeedbackAsync(feedback);
            
            _logger.LogDebug("User feedback recorded for {Filename}: {ActualCategory}", filename, actualCategory);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record user feedback for {Filename}, continuing without ML training update", filename);
            return Result<Unit>.Success(Unit.Value); // Gracefully ignore feedback failures
        }
    }
}

/// <summary>
/// File classification suggestion with fallback information.
/// </summary>
public class FileClassificationSuggestion
{
    /// <summary>
    /// Original filename being classified.
    /// </summary>
    public required string Filename { get; init; }

    /// <summary>
    /// Suggested category from ML or null if unavailable.
    /// </summary>
    public string? SuggestedCategory { get; init; }

    /// <summary>
    /// Confidence score from 0.0 to 1.0.
    /// </summary>
    public float Confidence { get; init; }

    /// <summary>
    /// Alternative category suggestions.
    /// </summary>
    public IReadOnlyList<CategorySuggestion> AlternativeCategories { get; init; } = Array.Empty<CategorySuggestion>();

    /// <summary>
    /// Whether manual review is required.
    /// </summary>
    public bool RequiresManualReview { get; init; }

    /// <summary>
    /// Reason for fallback if ML was unavailable.
    /// </summary>
    public string? FallbackReason { get; init; }

    /// <summary>
    /// Processing time in milliseconds.
    /// </summary>
    public int ProcessingTimeMs { get; init; }
}

/// <summary>
/// Category suggestion with confidence.
/// </summary>
public class CategorySuggestion
{
    /// <summary>
    /// Category name.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Confidence score from 0.0 to 1.0.
    /// </summary>
    public float Confidence { get; init; }
}