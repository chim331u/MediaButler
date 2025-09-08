using MediaButler.Core.Common;
using MediaButler.ML.Models;

namespace MediaButler.ML.Interfaces;

/// <summary>
/// Service for managing media categories with dynamic registry and normalization.
/// Handles category creation, normalization, and confidence thresholds for Italian content optimization.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Only handles category management
/// - Values over state: Immutable category definitions
/// - Compose don't complex: Independent from prediction logic
/// </remarks>
public interface ICategoryService
{
    /// <summary>
    /// Gets all registered categories with their metadata.
    /// </summary>
    /// <returns>Complete category registry with metadata</returns>
    Task<Result<CategoryRegistry>> GetCategoryRegistryAsync();

    /// <summary>
    /// Normalizes a category name according to established rules.
    /// </summary>
    /// <param name="categoryName">Raw category name to normalize</param>
    /// <returns>Normalized category name or failure if invalid</returns>
    Result<string> NormalizeCategory(string categoryName);

    /// <summary>
    /// Gets confidence threshold for a specific category.
    /// </summary>
    /// <param name="categoryName">Category to get threshold for</param>
    /// <returns>Confidence threshold (0.0-1.0) or failure if category not found</returns>
    Result<double> GetCategoryThreshold(string categoryName);

    /// <summary>
    /// Registers a new category with metadata and threshold.
    /// </summary>
    /// <param name="category">Category definition to register</param>
    /// <returns>Success or failure result</returns>
    Task<Result<CategoryDefinition>> RegisterCategoryAsync(CategoryDefinition category);

    /// <summary>
    /// Updates category metadata and threshold settings.
    /// </summary>
    /// <param name="categoryName">Category name to update</param>
    /// <param name="updates">Updates to apply</param>
    /// <returns>Updated category definition or failure</returns>
    Task<Result<CategoryDefinition>> UpdateCategoryAsync(string categoryName, CategoryUpdate updates);

    /// <summary>
    /// Gets suggested categories based on user feedback and usage patterns.
    /// </summary>
    /// <param name="filename">Filename to suggest categories for</param>
    /// <param name="maxSuggestions">Maximum number of suggestions to return</param>
    /// <returns>Ranked list of category suggestions</returns>
    Task<Result<CategorySuggestionResult>> GetCategorySuggestionsAsync(string filename, int maxSuggestions = 5);

    /// <summary>
    /// Records user feedback for category suggestions to improve future recommendations.
    /// </summary>
    /// <param name="feedback">User feedback on category prediction</param>
    /// <returns>Success or failure result</returns>
    Task<Result> RecordUserFeedbackAsync(CategoryFeedback feedback);

    /// <summary>
    /// Gets category statistics and usage metrics.
    /// </summary>
    /// <returns>Category usage statistics and metrics</returns>
    Task<Result<CategoryStatistics>> GetCategoryStatisticsAsync();

    /// <summary>
    /// Merges two categories, transferring all associations to the target category.
    /// </summary>
    /// <param name="sourceCategory">Category to merge from</param>
    /// <param name="targetCategory">Category to merge into</param>
    /// <returns>Merge operation result</returns>
    Task<Result<CategoryMergeResult>> MergeCategoriesAsync(string sourceCategory, string targetCategory);

    /// <summary>
    /// Validates category name according to naming conventions and rules.
    /// </summary>
    /// <param name="categoryName">Category name to validate</param>
    /// <returns>Validation result with issues if any</returns>
    Result<CategoryValidationResult> ValidateCategoryName(string categoryName);
}