using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using MediaButler.Core.Common;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;
using Microsoft.Extensions.Logging;

namespace MediaButler.ML.Services;

/// <summary>
/// Service for managing media categories with dynamic registry and normalization.
/// Handles category creation, normalization, confidence thresholds, and user feedback integration.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Only handles category management
/// - Values over state: Immutable category definitions with explicit updates
/// - Compose don't complex: Independent from prediction and file operations
/// - Thread-safe: ConcurrentDictionary for registry management
/// </remarks>
public sealed class CategoryService : ICategoryService
{
    private readonly ILogger<CategoryService> _logger;
    private readonly ITokenizerService _tokenizerService;
    
    // Thread-safe category storage
    private readonly ConcurrentDictionary<string, CategoryDefinition> _categoryRegistry;
    private readonly ConcurrentDictionary<string, string> _categoryAliases;
    private readonly ConcurrentQueue<CategoryFeedback> _feedbackQueue;
    
    // Italian content optimization patterns
    private readonly IReadOnlyDictionary<string, CategoryDefinition> _italianCategories;
    private readonly IReadOnlySet<string> _italianReleaseGroups;
    
    public CategoryService(
        ILogger<CategoryService> logger,
        ITokenizerService tokenizerService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tokenizerService = tokenizerService ?? throw new ArgumentNullException(nameof(tokenizerService));
        
        _categoryRegistry = new ConcurrentDictionary<string, CategoryDefinition>();
        _categoryAliases = new ConcurrentDictionary<string, string>();
        _feedbackQueue = new ConcurrentQueue<CategoryFeedback>();
        
        _italianCategories = InitializeItalianCategories();
        _italianReleaseGroups = InitializeItalianReleaseGroups();
        
        InitializeDefaultCategories();
        
        _logger.LogInformation("CategoryService initialized with {CategoryCount} default categories", 
            _categoryRegistry.Count);
    }
    
    public Task<Result<CategoryRegistry>> GetCategoryRegistryAsync()
    {
        try
        {
            var categories = _categoryRegistry.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var aliases = _categoryAliases.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    
            var registry = new CategoryRegistry
            {
                Categories = categories.AsReadOnly(),
                Aliases = aliases.AsReadOnly()
            };
    
            _logger.LogDebug("Retrieved category registry with {CategoryCount} categories and {AliasCount} aliases",
                categories.Count, aliases.Count);
    
            return Task.FromResult(Result<CategoryRegistry>.Success(registry));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve category registry");
            return Task.FromResult(Result<CategoryRegistry>.Failure($"Failed to retrieve category registry: {ex.Message}"));
        }
    }
    
    public Result<string> NormalizeCategory(string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            return Result<string>.Failure("Category name cannot be null or empty");
    
        try
        {
            var normalized = NormalizeCategoryInternal(categoryName);
            
            _logger.LogDebug("Normalized category '{Original}' to '{Normalized}'", categoryName, normalized);
            
            return Result<string>.Success(normalized);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to normalize category '{CategoryName}'", categoryName);
            return Result<string>.Failure($"Failed to normalize category: {ex.Message}");
        }
    }
    
    public Result<double> GetCategoryThreshold(string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            return Result<double>.Failure("Category name cannot be null or empty");
    
        var normalized = NormalizeCategoryInternal(categoryName);
        
        if (_categoryRegistry.TryGetValue(normalized, out var category))
        {
            return Result<double>.Success(category.ConfidenceThreshold);
        }
    
        // Check aliases
        if (_categoryAliases.TryGetValue(normalized, out var canonicalName) &&
            _categoryRegistry.TryGetValue(canonicalName, out var aliasedCategory))
        {
            return Result<double>.Success(aliasedCategory.ConfidenceThreshold);
        }
    
        _logger.LogWarning("Category '{CategoryName}' not found in registry", categoryName);
        return Result<double>.Failure($"Category '{categoryName}' not found");
    }
    
    public Task<Result<CategoryDefinition>> RegisterCategoryAsync(CategoryDefinition category)
    {
        if (category == null)
            return Task.FromResult(Result<CategoryDefinition>.Failure("Category definition cannot be null"));
    
        try
        {
            var validation = ValidateCategoryName(category.Name);
            if (!validation.IsSuccess)
                return Task.FromResult(Result<CategoryDefinition>.Failure($"Invalid category name: {validation.Error}"));
    
            var normalized = validation.Value.NormalizedName;
            
            if (_categoryRegistry.ContainsKey(normalized))
                return Task.FromResult(Result<CategoryDefinition>.Failure($"Category '{normalized}' already exists"));
    
            var newCategory = category with { Name = normalized, LastUpdated = DateTime.UtcNow };
            
            _categoryRegistry.TryAdd(normalized, newCategory);
    
            // Register aliases
            foreach (var alias in category.Aliases)
            {
                var normalizedAlias = NormalizeCategoryInternal(alias);
                _categoryAliases.TryAdd(normalizedAlias, normalized);
            }
    
            _logger.LogInformation("Registered new category '{CategoryName}' with {AliasCount} aliases",
                normalized, category.Aliases.Count);
    
            return Task.FromResult(Result<CategoryDefinition>.Success(newCategory));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register category '{CategoryName}'", category?.Name);
            return Task.FromResult(Result<CategoryDefinition>.Failure($"Failed to register category: {ex.Message}"));
        }
    }
    
    public Task<Result<CategoryDefinition>> UpdateCategoryAsync(string categoryName, CategoryUpdate updates)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            return Task.FromResult(Result<CategoryDefinition>.Failure("Category name cannot be null or empty"));
    
        if (updates == null)
            return Task.FromResult(Result<CategoryDefinition>.Failure("Updates cannot be null"));
    
        try
        {
            var normalized = NormalizeCategoryInternal(categoryName);
            
            if (!_categoryRegistry.TryGetValue(normalized, out var existingCategory))
                return Task.FromResult(Result<CategoryDefinition>.Failure($"Category '{categoryName}' not found"));
    
            var updatedCategory = ApplyUpdates(existingCategory, updates);
            
            _categoryRegistry.TryUpdate(normalized, updatedCategory, existingCategory);
    
            // Handle alias updates
            if (updates.NewAliases != null)
            {
                foreach (var alias in updates.NewAliases)
                {
                    var normalizedAlias = NormalizeCategoryInternal(alias);
                    _categoryAliases.TryAdd(normalizedAlias, normalized);
                }
            }
    
            if (updates.RemoveAliases != null)
            {
                foreach (var alias in updates.RemoveAliases)
                {
                    var normalizedAlias = NormalizeCategoryInternal(alias);
                    _categoryAliases.TryRemove(normalizedAlias, out _);
                }
            }
    
            _logger.LogInformation("Updated category '{CategoryName}'", normalized);
            
            return Task.FromResult(Result<CategoryDefinition>.Success(updatedCategory));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update category '{CategoryName}'", categoryName);
            return Task.FromResult(Result<CategoryDefinition>.Failure($"Failed to update category: {ex.Message}"));
        }
    }
    
    public Task<Result<CategorySuggestionResult>> GetCategorySuggestionsAsync(string filename, int maxSuggestions = 5)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return Task.FromResult(Result<CategorySuggestionResult>.Failure("Filename cannot be null or empty"));
    
        if (maxSuggestions <= 0)
            maxSuggestions = 5;
    
        try
        {
            var suggestions = new List<CategorySuggestion>();
    
            // Tokenize filename for analysis
            var tokenResult = _tokenizerService.TokenizeFilename(filename);
            if (!tokenResult.IsSuccess)
            {
                _logger.LogWarning("Failed to tokenize filename for suggestions: {Error}", tokenResult.Error);
                return Task.FromResult(Result<CategorySuggestionResult>.Success(new CategorySuggestionResult
                {
                    Filename = filename,
                    Suggestions = Array.Empty<CategorySuggestion>().AsReadOnly()
                }));
            }
    
            var tokenized = tokenResult.Value;
            var seriesTokens = tokenized.SeriesTokens.Select(t => t.ToUpperInvariant()).ToList();
    
            // Pattern-based suggestions
            suggestions.AddRange(GetPatternBasedSuggestions(filename, tokenized, seriesTokens));
    
            // Keyword matching suggestions
            suggestions.AddRange(GetKeywordBasedSuggestions(seriesTokens));
    
            // Italian content specific suggestions
            suggestions.AddRange(GetItalianContentSuggestions(filename, tokenized));
    
            // Similarity matching suggestions
            suggestions.AddRange(GetSimilarityBasedSuggestions(seriesTokens));
    
            // Rank and limit suggestions
            var rankedSuggestions = suggestions
                .GroupBy(s => s.CategoryName)
                .Select(g => g.OrderByDescending(s => s.Confidence).First())
                .OrderByDescending(s => s.Confidence)
                .Take(maxSuggestions)
                .ToList();
    
            var result = new CategorySuggestionResult
            {
                Filename = filename,
                Suggestions = rankedSuggestions.AsReadOnly()
            };
    
            _logger.LogDebug("Generated {SuggestionCount} suggestions for '{Filename}'",
                rankedSuggestions.Count, filename);
    
            return Task.FromResult(Result<CategorySuggestionResult>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get category suggestions for '{Filename}'", filename);
            return Task.FromResult(Result<CategorySuggestionResult>.Failure($"Failed to get suggestions: {ex.Message}"));
        }
    }
    
    public Task<Result> RecordUserFeedbackAsync(CategoryFeedback feedback)
    {
        if (feedback == null)
            return Task.FromResult(Result.Failure("Feedback cannot be null"));
    
        try
        {
            _feedbackQueue.Enqueue(feedback);
            
            _logger.LogInformation("Recorded user feedback for '{Filename}': predicted '{Predicted}', actual '{Actual}', correct: {Correct}",
                feedback.Filename, feedback.PredictedCategory, feedback.ActualCategory, feedback.WasPredictionCorrect);
    
            // Update category statistics based on feedback
            UpdateCategoryStatisticsFromFeedback(feedback);
    
            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record user feedback for '{Filename}'", feedback?.Filename);
            return Task.FromResult(Result.Failure($"Failed to record feedback: {ex.Message}"));
        }
    }
    
    public Task<Result<CategoryStatistics>> GetCategoryStatisticsAsync()
    {
        try
        {
            var categoryStats = new List<CategoryStats>();
            var totalFeedback = _feedbackQueue.Count;
            var correctPredictions = 0;
            var totalConfidence = 0.0;
    
            foreach (var category in _categoryRegistry.Values.Where(c => c.IsActive))
            {
                var feedbackForCategory = _feedbackQueue
                    .Where(f => f.ActualCategory.Equals(category.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();
    
                var userCorrections = feedbackForCategory.Count(f => !f.WasPredictionCorrect);
                var avgConfidence = feedbackForCategory.Any() 
                    ? feedbackForCategory.Average(f => f.PredictionConfidence) 
                    : category.AverageConfidence;
    
                var accuracy = feedbackForCategory.Any()
                    ? feedbackForCategory.Count(f => f.WasPredictionCorrect) / (double)feedbackForCategory.Count
                    : 1.0; // No feedback assumes perfect accuracy
    
                categoryStats.Add(new CategoryStats
                {
                    CategoryName = category.Name,
                    FileCount = category.FileCount,
                    AverageConfidence = avgConfidence,
                    PredictionAccuracy = accuracy,
                    UserCorrections = userCorrections
                });
    
                correctPredictions += feedbackForCategory.Count(f => f.WasPredictionCorrect);
                totalConfidence += avgConfidence * feedbackForCategory.Count;
            }
    
            var overallAccuracy = totalFeedback > 0 ? correctPredictions / (double)totalFeedback : 1.0;
            var averageConfidence = totalFeedback > 0 ? totalConfidence / totalFeedback : 0.8;
    
            var statistics = new CategoryStatistics
            {
                TotalCategories = _categoryRegistry.Count(kvp => kvp.Value.IsActive),
                CategoryStats = categoryStats.AsReadOnly(),
                OverallAccuracy = overallAccuracy,
                AverageConfidence = averageConfidence
            };
    
            _logger.LogDebug("Generated category statistics for {CategoryCount} categories with {FeedbackCount} feedback entries",
                statistics.TotalCategories, totalFeedback);
    
            return Task.FromResult(Result<CategoryStatistics>.Success(statistics));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate category statistics");
            return Task.FromResult(Result<CategoryStatistics>.Failure($"Failed to generate statistics: {ex.Message}"));
        }
    }
    
    public Task<Result<CategoryMergeResult>> MergeCategoriesAsync(string sourceCategory, string targetCategory)
    {
        if (string.IsNullOrWhiteSpace(sourceCategory) || string.IsNullOrWhiteSpace(targetCategory))
            return Task.FromResult(Result<CategoryMergeResult>.Failure("Source and target categories cannot be null or empty"));
    
        try
        {
            var sourceNormalized = NormalizeCategoryInternal(sourceCategory);
            var targetNormalized = NormalizeCategoryInternal(targetCategory);
    
            if (sourceNormalized.Equals(targetNormalized, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(Result<CategoryMergeResult>.Failure("Source and target categories cannot be the same"));
    
            if (!_categoryRegistry.TryGetValue(sourceNormalized, out var source))
                return Task.FromResult(Result<CategoryMergeResult>.Failure($"Source category '{sourceCategory}' not found"));
    
            if (!_categoryRegistry.TryGetValue(targetNormalized, out var target))
                return Task.FromResult(Result<CategoryMergeResult>.Failure($"Target category '{targetCategory}' not found"));
    
            // Deactivate source category
            var deactivatedSource = source with { IsActive = false, LastUpdated = DateTime.UtcNow };
            _categoryRegistry.TryUpdate(sourceNormalized, deactivatedSource, source);
    
            // Transfer aliases from source to target
            var transferredAliases = 0;
            var aliasesToTransfer = _categoryAliases
                .Where(kvp => kvp.Value.Equals(sourceNormalized, StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Key)
                .ToList();
    
            foreach (var alias in aliasesToTransfer)
            {
                _categoryAliases.TryUpdate(alias, targetNormalized, sourceNormalized);
                transferredAliases++;
            }
    
            // Update target category file count
            var updatedTarget = target with
            {
                FileCount = target.FileCount + source.FileCount,
                LastUpdated = DateTime.UtcNow
            };
            _categoryRegistry.TryUpdate(targetNormalized, updatedTarget, target);
    
            var result = new CategoryMergeResult
            {
                SourceCategory = sourceNormalized,
                TargetCategory = targetNormalized,
                FilesTransferred = source.FileCount,
                AliasesTransferred = transferredAliases
            };
    
            _logger.LogInformation("Merged category '{Source}' into '{Target}': {Files} files, {Aliases} aliases transferred",
                sourceNormalized, targetNormalized, result.FilesTransferred, result.AliasesTransferred);
    
            return Task.FromResult(Result<CategoryMergeResult>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to merge categories '{Source}' -> '{Target}'", sourceCategory, targetCategory);
            return Task.FromResult(Result<CategoryMergeResult>.Failure($"Failed to merge categories: {ex.Message}"));
        }
    }
    
    public Result<CategoryValidationResult> ValidateCategoryName(string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return Result<CategoryValidationResult>.Success(new CategoryValidationResult
            {
                IsValid = false,
                NormalizedName = string.Empty,
                Issues = new[] { "Category name cannot be null or empty" }.AsReadOnly()
            });
        }
    
        var issues = new List<string>();
        var suggestions = new List<string>();
        
        var normalized = NormalizeCategoryInternal(categoryName);
    
        // Length validation
        if (normalized.Length < 2)
        {
            issues.Add("Category name must be at least 2 characters long");
        }
        else if (normalized.Length > 100)
        {
            issues.Add("Category name must be no more than 100 characters long");
            suggestions.Add("Consider using abbreviations or shorter form");
        }
    
        // Character validation
        if (!Regex.IsMatch(normalized, @"^[A-Z0-9\s\-&'!]+$"))
        {
            issues.Add("Category name contains invalid characters");
            suggestions.Add("Use only letters, numbers, spaces, hyphens, ampersands, apostrophes, and exclamation marks");
        }
    
        // Reserved word validation
        var reservedWords = new[] { "NEW", "UNKNOWN", "OTHER", "TEMP", "TEST" };
        if (reservedWords.Contains(normalized))
        {
            issues.Add($"'{normalized}' is a reserved category name");
            suggestions.Add("Choose a more specific category name");
        }
    
        var result = new CategoryValidationResult
        {
            IsValid = issues.Count == 0,
            NormalizedName = normalized,
            Issues = issues.AsReadOnly(),
            Suggestions = suggestions.AsReadOnly()
        };
    
        return Result<CategoryValidationResult>.Success(result);
    }
    
    #region Private Helper Methods
    
    private string NormalizeCategoryInternal(string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            return string.Empty;
    
        // Convert to uppercase and trim
        var normalized = categoryName.Trim().ToUpperInvariant();
    
        // Replace common separators with spaces
        normalized = Regex.Replace(normalized, @"[._\-]+", " ");
    
        // Clean up multiple spaces
        normalized = Regex.Replace(normalized, @"\s+", " ");
    
        // Remove common prefixes/suffixes that don't add value
        normalized = Regex.Replace(normalized, @"^(THE\s+|A\s+)", "");
        normalized = Regex.Replace(normalized, @"\s+(TV|SERIES|SHOW)$", "");
    
        return normalized.Trim();
    }
    
    private void InitializeDefaultCategories()
    {
        foreach (var category in _italianCategories.Values)
        {
            _categoryRegistry.TryAdd(category.Name, category);
            
            foreach (var alias in category.Aliases)
            {
                _categoryAliases.TryAdd(NormalizeCategoryInternal(alias), category.Name);
            }
        }
    }
    
    private IReadOnlyDictionary<string, CategoryDefinition> InitializeItalianCategories()
    {
        var categories = new Dictionary<string, CategoryDefinition>();
    
        // Popular Italian TV Series
        categories["IL TRONO DI SPADE"] = CategoryDefinition.Create(
            "IL TRONO DI SPADE", "Il Trono di Spade", CategoryType.TVSeries, 0.9) with
        {
            Description = "Game of Thrones - Italian localization",
            Aliases = new[] { "GAME OF THRONES", "GOT" }.AsReadOnly(),
            Keywords = new[] { "TRONO", "SPADE", "GAME", "THRONES" }.AsReadOnly(),
            ItalianInfo = new ItalianCategoryInfo
            {
                OriginalTitle = "Game of Thrones",
                ItalianReleaseGroups = new[] { "NOVARIP", "DARKSIDEMUX" }.AsReadOnly(),
                LanguageIndicators = new[] { "ITA", "ITALIAN" }.AsReadOnly(),
                IsCommonInItaly = true
            }
        };
    
        categories["ONE PIECE"] = CategoryDefinition.Create(
            "ONE PIECE", "One Piece", CategoryType.Anime, 0.85) with
        {
            Description = "Popular anime series",
            Keywords = new[] { "ONE", "PIECE" }.AsReadOnly(),
            ItalianInfo = new ItalianCategoryInfo
            {
                ItalianReleaseGroups = new[] { "UBI", "PIR8" }.AsReadOnly(),
                LanguageIndicators = new[] { "SUB", "ITA" }.AsReadOnly(),
                IsCommonInItaly = true
            }
        };
    
        categories["MY HERO ACADEMIA"] = CategoryDefinition.Create(
            "MY HERO ACADEMIA", "My Hero Academia", CategoryType.Anime, 0.85) with
        {
            Description = "Boku no Hero Academia anime series",
            Aliases = new[] { "BOKU NO HERO ACADEMIA", "MHA" }.AsReadOnly(),
            Keywords = new[] { "MY", "HERO", "ACADEMIA", "BOKU", "NO" }.AsReadOnly(),
            ItalianInfo = new ItalianCategoryInfo
            {
                OriginalTitle = "Boku no Hero Academia",
                ItalianReleaseGroups = new[] { "PIR8", "DARKSIDEMUX" }.AsReadOnly(),
                LanguageIndicators = new[] { "SUB", "ITA" }.AsReadOnly(),
                IsCommonInItaly = true
            }
        };
    
        categories["BREAKING BAD"] = CategoryDefinition.Create(
            "BREAKING BAD", "Breaking Bad", CategoryType.TVSeries, 0.9) with
        {
            Keywords = new[] { "BREAKING", "BAD" }.AsReadOnly(),
            ItalianInfo = new ItalianCategoryInfo
            {
                ItalianReleaseGroups = new[] { "NOVARIP", "MEM" }.AsReadOnly(),
                IsCommonInItaly = true
            }
        };
    
        categories["THE OFFICE"] = CategoryDefinition.Create(
            "THE OFFICE", "The Office", CategoryType.TVSeries, 0.85) with
        {
            Keywords = new[] { "OFFICE" }.AsReadOnly(),
            ItalianInfo = new ItalianCategoryInfo
            {
                ItalianReleaseGroups = new[] { "NOVARIP" }.AsReadOnly(),
                IsCommonInItaly = false
            }
        };
    
        return categories.AsReadOnly();
    }
    
    private IReadOnlySet<string> InitializeItalianReleaseGroups()
    {
        return new HashSet<string>
        {
            "NOVARIP", "DARKSIDEMUX", "PIR8", "MEM", "UBI", "BLUWORLD",
            "MORPHEUS", "IGM", "BVG", "LIFE", "MILLENNIUM"
        }.ToHashSet();
    }
    
    private List<CategorySuggestion> GetPatternBasedSuggestions(string filename, TokenizedFilename tokenized, List<string> seriesTokens)
    {
        var suggestions = new List<CategorySuggestion>();
    
        foreach (var category in _categoryRegistry.Values.Where(c => c.IsActive))
        {
            var matchScore = CalculatePatternMatch(seriesTokens, category);
            if (matchScore > 0.3)
            {
                var reasons = new List<string>();
                var matchingElements = new List<string>();
    
                if (matchScore > 0.8)
                    reasons.Add("High pattern match with series name");
                else if (matchScore > 0.5)
                    reasons.Add("Partial pattern match with series name");
                else
                    reasons.Add("Low pattern match with series name");
    
                // Check for Italian release groups
                if (category.ItalianInfo != null && HasItalianReleaseGroup(filename))
                {
                    matchScore += 0.1;
                    reasons.Add("Italian release group detected");
                    matchingElements.Add("Italian release group");
                }
    
                suggestions.Add(new CategorySuggestion
                {
                    CategoryName = category.Name,
                    Confidence = Math.Min(matchScore, 1.0),
                    Reasons = reasons.AsReadOnly(),
                    MatchingElements = matchingElements.AsReadOnly(),
                    Source = SuggestionSource.PatternMatch
                });
            }
        }
    
        return suggestions;
    }
    
    private List<CategorySuggestion> GetKeywordBasedSuggestions(List<string> seriesTokens)
    {
        var suggestions = new List<CategorySuggestion>();
    
        foreach (var category in _categoryRegistry.Values.Where(c => c.IsActive && c.Keywords.Any()))
        {
            var matchingKeywords = category.Keywords
                .Where(k => seriesTokens.Any(t => t.Contains(k, StringComparison.OrdinalIgnoreCase)))
                .ToList();
    
            if (matchingKeywords.Any())
            {
                var confidence = Math.Min(matchingKeywords.Count / (double)category.Keywords.Count, 1.0);
                confidence = Math.Max(confidence, 0.4); // Minimum keyword confidence
    
                suggestions.Add(new CategorySuggestion
                {
                    CategoryName = category.Name,
                    Confidence = confidence,
                    Reasons = new[] { $"Matched {matchingKeywords.Count} keywords" }.AsReadOnly(),
                    MatchingElements = matchingKeywords.AsReadOnly(),
                    Source = SuggestionSource.KeywordMatch
                });
            }
        }
    
        return suggestions;
    }
    
    private List<CategorySuggestion> GetItalianContentSuggestions(string filename, TokenizedFilename tokenized)
    {
        var suggestions = new List<CategorySuggestion>();
    
        if (!HasItalianLanguageIndicators(filename))
            return suggestions;
    
        foreach (var category in _categoryRegistry.Values
            .Where(c => c.IsActive && c.ItalianInfo != null && c.ItalianInfo.IsCommonInItaly))
        {
            var confidence = 0.5; // Base confidence for Italian content
            var reasons = new List<string> { "Italian content detected" };
            var matchingElements = new List<string>();
    
            // Check Italian release groups
            var releaseGroup = tokenized.ReleaseGroup?.ToUpperInvariant();
            if (!string.IsNullOrEmpty(releaseGroup) && 
                category.ItalianInfo!.ItalianReleaseGroups.Contains(releaseGroup))
            {
                confidence += 0.2;
                reasons.Add($"Matching Italian release group: {releaseGroup}");
                matchingElements.Add(releaseGroup);
            }
    
            // Check Italian naming patterns
            foreach (var pattern in category.ItalianInfo!.NamingPatterns)
            {
                if (filename.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    confidence += 0.1;
                    reasons.Add($"Italian naming pattern: {pattern}");
                    matchingElements.Add(pattern);
                }
            }
    
            if (confidence > 0.5)
            {
                suggestions.Add(new CategorySuggestion
                {
                    CategoryName = category.Name,
                    Confidence = Math.Min(confidence, 1.0),
                    Reasons = reasons.AsReadOnly(),
                    MatchingElements = matchingElements.AsReadOnly(),
                    Source = SuggestionSource.PatternMatch
                });
            }
        }
    
        return suggestions;
    }
    
    private List<CategorySuggestion> GetSimilarityBasedSuggestions(List<string> seriesTokens)
    {
        var suggestions = new List<CategorySuggestion>();
    
        foreach (var category in _categoryRegistry.Values.Where(c => c.IsActive))
        {
            var categoryTokens = category.Name.Split(' ').Select(t => t.ToUpperInvariant()).ToList();
            var similarity = CalculateTokenSimilarity(seriesTokens, categoryTokens);
    
            if (similarity > 0.4)
            {
                suggestions.Add(new CategorySuggestion
                {
                    CategoryName = category.Name,
                    Confidence = similarity,
                    Reasons = new[] { "Token similarity match" }.AsReadOnly(),
                    MatchingElements = Array.Empty<string>().AsReadOnly(),
                    Source = SuggestionSource.SimilarityMatch
                });
            }
        }
    
        return suggestions;
    }
    
    private double CalculatePatternMatch(List<string> seriesTokens, CategoryDefinition category)
    {
        var categoryTokens = category.Name.Split(' ').Select(t => t.ToUpperInvariant()).ToList();
        
        var matchingTokens = seriesTokens.Intersect(categoryTokens, StringComparer.OrdinalIgnoreCase).Count();
        var totalTokens = Math.Max(seriesTokens.Count, categoryTokens.Count);
    
        return totalTokens > 0 ? matchingTokens / (double)totalTokens : 0.0;
    }
    
    private double CalculateTokenSimilarity(List<string> tokens1, List<string> tokens2)
    {
        if (!tokens1.Any() || !tokens2.Any())
            return 0.0;
    
        var intersection = tokens1.Intersect(tokens2, StringComparer.OrdinalIgnoreCase).Count();
        var union = tokens1.Union(tokens2, StringComparer.OrdinalIgnoreCase).Count();
    
        return union > 0 ? intersection / (double)union : 0.0;
    }
    
    private bool HasItalianLanguageIndicators(string filename)
    {
        var italianIndicators = new[] { "ITA", "ITALIAN", "SUB.ITA", "DUB.ITA" };
        return italianIndicators.Any(indicator => 
            filename.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }
    
    private bool HasItalianReleaseGroup(string filename)
    {
        return _italianReleaseGroups.Any(group => 
            filename.Contains(group, StringComparison.OrdinalIgnoreCase));
    }
    
    private CategoryDefinition ApplyUpdates(CategoryDefinition existing, CategoryUpdate updates)
    {
        var aliases = existing.Aliases.ToList();
        var keywords = existing.Keywords.ToList();
    
        if (updates.NewAliases != null)
            aliases.AddRange(updates.NewAliases);
        if (updates.RemoveAliases != null)
            aliases.RemoveAll(a => updates.RemoveAliases.Contains(a, StringComparer.OrdinalIgnoreCase));
    
        if (updates.NewKeywords != null)
            keywords.AddRange(updates.NewKeywords);
        if (updates.RemoveKeywords != null)
            keywords.RemoveAll(k => updates.RemoveKeywords.Contains(k, StringComparer.OrdinalIgnoreCase));
    
        return existing with
        {
            DisplayName = updates.DisplayName ?? existing.DisplayName,
            Description = updates.Description ?? existing.Description,
            ConfidenceThreshold = updates.ConfidenceThreshold ?? existing.ConfidenceThreshold,
            Aliases = aliases.AsReadOnly(),
            Keywords = keywords.AsReadOnly(),
            Organization = updates.Organization ?? existing.Organization,
            IsActive = updates.IsActive ?? existing.IsActive,
            LastUpdated = DateTime.UtcNow
        };
    }
    
    private void UpdateCategoryStatisticsFromFeedback(CategoryFeedback feedback)
    {
        // Update predicted category statistics (if wrong)
        if (!feedback.WasPredictionCorrect)
        {
            var predictedNormalized = NormalizeCategoryInternal(feedback.PredictedCategory);
            if (_categoryRegistry.TryGetValue(predictedNormalized, out var predicted))
            {
                // This could trigger a confidence threshold adjustment in the future
                _logger.LogDebug("Incorrect prediction for category '{Category}', confidence was {Confidence}",
                    predictedNormalized, feedback.PredictionConfidence);
            }
        }
    
        // Update actual category statistics
        var actualNormalized = NormalizeCategoryInternal(feedback.ActualCategory);
        if (_categoryRegistry.TryGetValue(actualNormalized, out var actual))
        {
            var updatedActual = actual with
            {
                FileCount = actual.FileCount + 1,
                LastUpdated = DateTime.UtcNow
            };
            
            _categoryRegistry.TryUpdate(actualNormalized, updatedActual, actual);
        }
    }

   #endregion
}