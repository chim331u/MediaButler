using System.ComponentModel.DataAnnotations;

namespace MediaButler.ML.Models;

/// <summary>
/// Complete registry of all categories with their definitions and metadata.
/// </summary>
public sealed record CategoryRegistry
{
    /// <summary>
    /// All registered categories indexed by normalized name.
    /// </summary>
    public required IReadOnlyDictionary<string, CategoryDefinition> Categories { get; init; }

    /// <summary>
    /// Category aliases mapping alternative names to canonical names.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Aliases { get; init; }

    /// <summary>
    /// Total number of registered categories.
    /// </summary>
    public int TotalCategories => Categories.Count;

    /// <summary>
    /// When the registry was last updated.
    /// </summary>
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets category by name (supports aliases).
    /// </summary>
    /// <param name="categoryName">Category name or alias</param>
    /// <returns>Category definition if found</returns>
    public CategoryDefinition? GetCategory(string categoryName)
    {
        var normalized = categoryName.ToUpperInvariant().Trim();
        
        // Direct lookup
        if (Categories.TryGetValue(normalized, out var category))
            return category;
            
        // Alias lookup
        if (Aliases.TryGetValue(normalized, out var canonicalName))
            return Categories.GetValueOrDefault(canonicalName);
            
        return null;
    }

    /// <summary>
    /// Checks if category exists (including aliases).
    /// </summary>
    /// <param name="categoryName">Category name to check</param>
    /// <returns>True if category exists</returns>
    public bool CategoryExists(string categoryName)
    {
        return GetCategory(categoryName) != null;
    }
}

/// <summary>
/// Definition of a media category with metadata and classification settings.
/// </summary>
public sealed record CategoryDefinition
{
    /// <summary>
    /// Canonical category name (uppercase, normalized).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Display name for user interfaces.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Optional description of the category.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Category type (TV series, movie, anime, etc.).
    /// </summary>
    public required CategoryType Type { get; init; }

    /// <summary>
    /// Confidence threshold for auto-classification (0.0-1.0).
    /// </summary>
    [Range(0.0, 1.0)]
    public required double ConfidenceThreshold { get; init; }

    /// <summary>
    /// Alternative names and aliases for this category.
    /// </summary>
    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Keywords that help identify this category.
    /// </summary>
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Italian-specific information if applicable.
    /// </summary>
    public ItalianCategoryInfo? ItalianInfo { get; init; }

    /// <summary>
    /// Organization preferences for this category.
    /// </summary>
    public CategoryOrganization? Organization { get; init; }

    /// <summary>
    /// When this category was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When this category was last updated.
    /// </summary>
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this category is actively used.
    /// </summary>
    public bool IsActive { get; init; } = true;

    /// <summary>
    /// Number of files currently classified under this category.
    /// </summary>
    public int FileCount { get; init; }

    /// <summary>
    /// Average confidence score for files in this category.
    /// </summary>
    [Range(0.0, 1.0)]
    public double AverageConfidence { get; init; }

    /// <summary>
    /// Creates a new category definition with required fields.
    /// </summary>
    /// <param name="name">Canonical category name</param>
    /// <param name="displayName">Display name</param>
    /// <param name="type">Category type</param>
    /// <param name="confidenceThreshold">Classification threshold</param>
    /// <returns>New category definition</returns>
    public static CategoryDefinition Create(string name, string displayName, CategoryType type, double confidenceThreshold = 0.8)
    {
        return new CategoryDefinition
        {
            Name = name.ToUpperInvariant().Trim(),
            DisplayName = displayName.Trim(),
            Type = type,
            ConfidenceThreshold = Math.Clamp(confidenceThreshold, 0.0, 1.0)
        };
    }
}

/// <summary>
/// Updates to apply to a category definition.
/// </summary>
public sealed record CategoryUpdate
{
    /// <summary>
    /// New display name (optional).
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// New description (optional).
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// New confidence threshold (optional).
    /// </summary>
    [Range(0.0, 1.0)]
    public double? ConfidenceThreshold { get; init; }

    /// <summary>
    /// New aliases to add (optional).
    /// </summary>
    public IReadOnlyList<string>? NewAliases { get; init; }

    /// <summary>
    /// Aliases to remove (optional).
    /// </summary>
    public IReadOnlyList<string>? RemoveAliases { get; init; }

    /// <summary>
    /// New keywords to add (optional).
    /// </summary>
    public IReadOnlyList<string>? NewKeywords { get; init; }

    /// <summary>
    /// Keywords to remove (optional).
    /// </summary>
    public IReadOnlyList<string>? RemoveKeywords { get; init; }

    /// <summary>
    /// New organization settings (optional).
    /// </summary>
    public CategoryOrganization? Organization { get; init; }

    /// <summary>
    /// Whether to activate/deactivate the category.
    /// </summary>
    public bool? IsActive { get; init; }
}

/// <summary>
/// Italian-specific category information for localized content.
/// </summary>
public sealed record ItalianCategoryInfo
{
    /// <summary>
    /// Original Italian title if different from canonical name.
    /// </summary>
    public string? OriginalTitle { get; init; }

    /// <summary>
    /// Common Italian release groups for this series.
    /// </summary>
    public IReadOnlyList<string> ItalianReleaseGroups { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Italian language indicators commonly used.
    /// </summary>
    public IReadOnlyList<string> LanguageIndicators { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Special Italian naming patterns for this series.
    /// </summary>
    public IReadOnlyList<string> NamingPatterns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Whether this series is commonly available in Italian.
    /// </summary>
    public bool IsCommonInItaly { get; init; } = true;
}

/// <summary>
/// Organization preferences for a category.
/// </summary>
public sealed record CategoryOrganization
{
    /// <summary>
    /// Custom folder name template.
    /// </summary>
    public string? FolderNameTemplate { get; init; }

    /// <summary>
    /// Whether to organize by seasons.
    /// </summary>
    public bool OrganizeBySeason { get; init; } = false;

    /// <summary>
    /// Whether to separate special episodes.
    /// </summary>
    public bool SeparateSpecials { get; init; } = false;

    /// <summary>
    /// Custom file naming pattern.
    /// </summary>
    public string? FileNamingPattern { get; init; }

    /// <summary>
    /// Quality-based subfolder organization.
    /// </summary>
    public bool OrganizeByQuality { get; init; } = false;
}

/// <summary>
/// Result of category suggestion operation.
/// </summary>
public sealed record CategorySuggestionResult
{
    /// <summary>
    /// Ordered list of category suggestions.
    /// </summary>
    public required IReadOnlyList<CategorySuggestion> Suggestions { get; init; }

    /// <summary>
    /// Filename that was analyzed.
    /// </summary>
    public required string Filename { get; init; }

    /// <summary>
    /// Analysis timestamp.
    /// </summary>
    public DateTime AnalyzedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Whether any high-confidence suggestions were found.
    /// </summary>
    public bool HasHighConfidenceSuggestions => 
        Suggestions.Any(s => s.Confidence >= 0.85);

    /// <summary>
    /// Best suggestion if any.
    /// </summary>
    public CategorySuggestion? BestSuggestion => 
        Suggestions.FirstOrDefault();
}

/// <summary>
/// Individual category suggestion with confidence and reasoning.
/// </summary>
public sealed record CategorySuggestion
{
    /// <summary>
    /// Suggested category name.
    /// </summary>
    public required string CategoryName { get; init; }

    /// <summary>
    /// Confidence score for this suggestion (0.0-1.0).
    /// </summary>
    [Range(0.0, 1.0)]
    public required double Confidence { get; init; }

    /// <summary>
    /// Reasons why this category was suggested.
    /// </summary>
    public required IReadOnlyList<string> Reasons { get; init; }

    /// <summary>
    /// Matching keywords or patterns found.
    /// </summary>
    public IReadOnlyList<string> MatchingElements { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Source of this suggestion.
    /// </summary>
    public required SuggestionSource Source { get; init; }

    /// <summary>
    /// Whether this suggestion requires user confirmation.
    /// </summary>
    public bool RequiresConfirmation => Confidence < 0.85;
}

/// <summary>
/// User feedback on category predictions for machine learning improvement.
/// </summary>
public sealed record CategoryFeedback
{
    /// <summary>
    /// Original filename that was classified.
    /// </summary>
    public required string Filename { get; init; }

    /// <summary>
    /// Category that was predicted by the system.
    /// </summary>
    public required string PredictedCategory { get; init; }

    /// <summary>
    /// Confidence of the original prediction.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double PredictionConfidence { get; init; }

    /// <summary>
    /// Category chosen by the user (may be different from predicted).
    /// </summary>
    public required string ActualCategory { get; init; }

    /// <summary>
    /// Whether the user accepted the prediction.
    /// </summary>
    public bool WasPredictionCorrect => 
        PredictedCategory.Equals(ActualCategory, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Optional user comments about the prediction.
    /// </summary>
    public string? Comments { get; init; }

    /// <summary>
    /// When the feedback was provided.
    /// </summary>
    public DateTime ProvidedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Source of the feedback.
    /// </summary>
    public required FeedbackSource Source { get; init; }
}

/// <summary>
/// Statistics about category usage and performance.
/// </summary>
public sealed record CategoryStatistics
{
    /// <summary>
    /// Total number of active categories.
    /// </summary>
    public required int TotalCategories { get; init; }

    /// <summary>
    /// Statistics per category.
    /// </summary>
    public required IReadOnlyList<CategoryStats> CategoryStats { get; init; }

    /// <summary>
    /// Overall prediction accuracy.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double OverallAccuracy { get; init; }

    /// <summary>
    /// Average confidence across all predictions.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double AverageConfidence { get; init; }

    /// <summary>
    /// Most popular categories by file count.
    /// </summary>
    public IReadOnlyList<CategoryStats> MostPopular => 
        CategoryStats.OrderByDescending(c => c.FileCount).ToList().AsReadOnly();

    /// <summary>
    /// Categories with highest accuracy.
    /// </summary>
    public IReadOnlyList<CategoryStats> HighestAccuracy => 
        CategoryStats.OrderByDescending(c => c.PredictionAccuracy).ToList().AsReadOnly();

    /// <summary>
    /// When these statistics were generated.
    /// </summary>
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Statistics for an individual category.
/// </summary>
public sealed record CategoryStats
{
    /// <summary>
    /// Category name.
    /// </summary>
    public required string CategoryName { get; init; }

    /// <summary>
    /// Number of files in this category.
    /// </summary>
    public required int FileCount { get; init; }

    /// <summary>
    /// Average prediction confidence for this category.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double AverageConfidence { get; init; }

    /// <summary>
    /// Prediction accuracy based on user feedback.
    /// </summary>
    [Range(0.0, 1.0)]
    public required double PredictionAccuracy { get; init; }

    /// <summary>
    /// Number of user corrections for this category.
    /// </summary>
    public required int UserCorrections { get; init; }

    /// <summary>
    /// Last time a file was added to this category.
    /// </summary>
    public DateTime? LastActivity { get; init; }
}

/// <summary>
/// Result of merging two categories.
/// </summary>
public sealed record CategoryMergeResult
{
    /// <summary>
    /// Source category that was merged (now inactive).
    /// </summary>
    public required string SourceCategory { get; init; }

    /// <summary>
    /// Target category that received all files.
    /// </summary>
    public required string TargetCategory { get; init; }

    /// <summary>
    /// Number of files transferred.
    /// </summary>
    public required int FilesTransferred { get; init; }

    /// <summary>
    /// Number of aliases transferred.
    /// </summary>
    public required int AliasesTransferred { get; init; }

    /// <summary>
    /// When the merge was completed.
    /// </summary>
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Result of category name validation.
/// </summary>
public sealed record CategoryValidationResult
{
    /// <summary>
    /// Whether the category name is valid.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Normalized version of the category name.
    /// </summary>
    public required string NormalizedName { get; init; }

    /// <summary>
    /// Validation issues if any.
    /// </summary>
    public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Suggestions for improvement if invalid.
    /// </summary>
    public IReadOnlyList<string> Suggestions { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Types of media categories.
/// </summary>
public enum CategoryType
{
    /// <summary>
    /// TV series (episodic content).
    /// </summary>
    TVSeries = 0,

    /// <summary>
    /// Movie (standalone content).
    /// </summary>
    Movie = 1,

    /// <summary>
    /// Anime series.
    /// </summary>
    Anime = 2,

    /// <summary>
    /// Documentary content.
    /// </summary>
    Documentary = 3,

    /// <summary>
    /// Mini-series or limited series.
    /// </summary>
    MiniSeries = 4,

    /// <summary>
    /// Other/unknown category type.
    /// </summary>
    Other = 5
}

/// <summary>
/// Source of a category suggestion.
/// </summary>
public enum SuggestionSource
{
    /// <summary>
    /// ML model prediction.
    /// </summary>
    MLPrediction = 0,

    /// <summary>
    /// Keyword matching.
    /// </summary>
    KeywordMatch = 1,

    /// <summary>
    /// Pattern matching.
    /// </summary>
    PatternMatch = 2,

    /// <summary>
    /// User history/preferences.
    /// </summary>
    UserHistory = 3,

    /// <summary>
    /// Similar filename analysis.
    /// </summary>
    SimilarityMatch = 4
}

/// <summary>
/// Source of user feedback.
/// </summary>
public enum FeedbackSource
{
    /// <summary>
    /// Direct user correction in UI.
    /// </summary>
    UserCorrection = 0,

    /// <summary>
    /// User confirmation of prediction.
    /// </summary>
    UserConfirmation = 1,

    /// <summary>
    /// Bulk operation feedback.
    /// </summary>
    BulkOperation = 2,

    /// <summary>
    /// API-provided feedback.
    /// </summary>
    ApiFeedback = 3
}