using System.Collections.Concurrent;
using System.Diagnostics;
using MediaButler.Core.Common;
using MediaButler.ML.Configuration;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediaButler.ML.Services;

/// <summary>
/// Service for ML model prediction and classification operations.
/// Provides thread-safe prediction capabilities optimized for Italian TV series classification.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Values over state: Immutable prediction results with explicit data flow
/// - Single responsibility: Only handles prediction operations, delegates other concerns
/// - Compose don't complect: Uses existing services without tight coupling
/// - Simple dependencies: Clear interfaces to tokenizer, feature engineering, and model services
/// </remarks>
public sealed class PredictionService : IPredictionService
{
    private readonly ILogger<PredictionService> _logger;
    private readonly ITokenizerService _tokenizerService;
    private readonly IFeatureEngineeringService _featureService;
    private readonly IModelTrainingService _trainingService;
    private readonly MLConfiguration _config;
    
    // Thread-safe statistics tracking
    private readonly ConcurrentDictionary<string, long> _predictionStats = new();
    private readonly ConcurrentQueue<PredictionMetric> _recentPredictions = new();
    private const int MaxRecentPredictions = 1000;

    public PredictionService(
        ILogger<PredictionService> logger,
        ITokenizerService tokenizerService,
        IFeatureEngineeringService featureService,
        IModelTrainingService trainingService,
        IOptions<MLConfiguration> config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tokenizerService = tokenizerService ?? throw new ArgumentNullException(nameof(tokenizerService));
        _featureService = featureService ?? throw new ArgumentNullException(nameof(featureService));
        _trainingService = trainingService ?? throw new ArgumentNullException(nameof(trainingService));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));

        // Initialize statistics counters
        _predictionStats["total"] = 0;
        _predictionStats["successful"] = 0;
        _predictionStats["failed"] = 0;
    }

    /// <inheritdoc />
    public Task<Result<ClassificationResult>> PredictAsync(
        string filename, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return Task.FromResult(Result<ClassificationResult>.Failure("Filename cannot be null or empty"));
        }

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogDebug("Starting prediction for filename: {Filename}", filename);

            // Step 1: Tokenize the filename
            var tokenizeResult = _tokenizerService.TokenizeFilename(filename);
            if (!tokenizeResult.IsSuccess)
            {
                return Task.FromResult(Result<ClassificationResult>.Failure($"Tokenization failed: {tokenizeResult.Error}"));
            }

            // Step 2: Extract features from tokens
            var featureResult = _featureService.ExtractFeatures(tokenizeResult.Value);
            if (!featureResult.IsSuccess)
            {
                return Task.FromResult(Result<ClassificationResult>.Failure($"Feature extraction failed: {featureResult.Error}"));
            }

            // Step 3: Make prediction using feature analysis
            var predictionResult = MakePrediction(filename, tokenizeResult.Value, featureResult.Value);

            stopwatch.Stop();

            // Create classification result with Italian content analysis
            var classificationResult = CreateClassificationResult(
                filename, 
                tokenizeResult.Value, 
                predictionResult, 
                stopwatch.Elapsed);

            // Update statistics
            RecordPredictionMetric(classificationResult, stopwatch.Elapsed);
            _predictionStats.AddOrUpdate("total", 1, (key, value) => value + 1);
            _predictionStats.AddOrUpdate("successful", 1, (key, value) => value + 1);

            _logger.LogInformation(
                "Prediction completed for {Filename} in {Duration}ms. Category: {Category}, Confidence: {Confidence:F2}",
                filename, stopwatch.ElapsedMilliseconds, classificationResult.PredictedCategory, classificationResult.Confidence);

            return Task.FromResult(Result<ClassificationResult>.Success(classificationResult));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Prediction cancelled for filename: {Filename}", filename);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _predictionStats.AddOrUpdate("total", 1, (key, value) => value + 1);
            _predictionStats.AddOrUpdate("failed", 1, (key, value) => value + 1);

            _logger.LogError(ex, "Prediction failed for filename: {Filename}", filename);
            return Task.FromResult(Result<ClassificationResult>.Failure($"Prediction error: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    public async Task<Result<BatchClassificationResult>> PredictBatchAsync(
        IEnumerable<string> filenames, 
        CancellationToken cancellationToken = default)
    {
        if (filenames == null)
        {
            return Result<BatchClassificationResult>.Failure("Filenames collection cannot be null");
        }

        var filenameList = filenames.ToList();
        if (!filenameList.Any())
        {
            return Result<BatchClassificationResult>.Failure("Filenames collection cannot be empty");
        }

        var batchStopwatch = Stopwatch.StartNew();
        var results = new List<ClassificationResult>();

        _logger.LogInformation("Starting batch prediction for {Count} filenames", filenameList.Count);

        try
        {
            // Process files concurrently with limited parallelism for ARM32 compatibility
            var semaphore = new SemaphoreSlim(Environment.ProcessorCount);
            var tasks = filenameList.Select(async filename =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await PredictAsync(filename, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var predictionResults = await Task.WhenAll(tasks);

            // Collect successful results and create failed results for errors
            foreach (var (result, index) in predictionResults.Select((r, i) => (r, i)))
            {
                if (result.IsSuccess)
                {
                    results.Add(result.Value);
                }
                else
                {
                    // Create a failed classification result for consistency
                    var failedResult = new ClassificationResult
                    {
                        Filename = filenameList[index],
                        PredictedCategory = "UNKNOWN",
                        Confidence = 0.0f,
                        Decision = ClassificationDecision.Failed,
                        ProcessingTimeMs = 0,
                        ModelVersion = "1.0.0-pattern-based"
                    };
                    results.Add(failedResult);
                }
            }

            batchStopwatch.Stop();

            var batchResult = new BatchClassificationResult
            {
                Results = results.AsReadOnly(),
                ProcessingDuration = batchStopwatch.Elapsed
            };

            _logger.LogInformation(
                "Batch prediction completed for {Total} files in {Duration}ms. Success: {Successful}/{Total}",
                batchResult.TotalFiles, batchStopwatch.ElapsedMilliseconds,
                batchResult.SuccessfulClassifications, batchResult.TotalFiles);

            return Result<BatchClassificationResult>.Success(batchResult);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Batch prediction cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch prediction failed");
            return Result<BatchClassificationResult>.Failure($"Batch prediction error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Task<Result<FilenameValidationResult>> ValidateFilenameAsync(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return Task.FromResult(Result<FilenameValidationResult>.Failure("Filename cannot be null or empty"));
        }

        try
        {
            var issues = new List<ValidationIssue>();
            var recommendations = new List<string>();

            // Basic validation checks
            if (filename.Length > 255)
            {
                issues.Add(new ValidationIssue 
                { 
                    Severity = IssueSeverity.Warning, 
                    Description = "Filename is very long and may cause processing issues" 
                });
            }

            if (filename.Any(c => Path.GetInvalidFileNameChars().Contains(c)))
            {
                issues.Add(new ValidationIssue 
                { 
                    Severity = IssueSeverity.Error, 
                    Description = "Filename contains invalid characters" 
                });
                recommendations.Add("Remove or replace invalid characters with valid alternatives");
            }

            // Analyze complexity
            var complexity = AnalyzeFilenameComplexity(filename);
            
            // Analyze Italian content
            var italianIndicators = AnalyzeItalianContent(filename);

            // Determine processing confidence
            var processingConfidence = CalculateProcessingConfidence(filename, complexity, italianIndicators, issues);

            var validationResult = new FilenameValidationResult
            {
                IsValid = !issues.Any(i => i.Severity == IssueSeverity.Error || i.Severity == IssueSeverity.Critical),
                ProcessingConfidence = processingConfidence,
                Issues = issues.AsReadOnly(),
                Recommendations = recommendations.AsReadOnly(),
                Complexity = complexity,
                ItalianIndicators = italianIndicators
            };

            return Task.FromResult(Result<FilenameValidationResult>.Success(validationResult));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Filename validation failed for: {Filename}", filename);
            return Task.FromResult(Result<FilenameValidationResult>.Failure($"Validation error: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    public Task<Result<PredictionPerformanceStats>> GetPerformanceStatsAsync()
    {
        try
        {
            var recentMetrics = GetRecentMetrics();
            var avgPredictionTime = recentMetrics.Any() 
                ? TimeSpan.FromTicks((long)recentMetrics.Average(m => m.Duration.Ticks))
                : TimeSpan.Zero;

            var avgConfidence = recentMetrics.Any() 
                ? recentMetrics.Average(m => m.Confidence)
                : 0.0;

            var confidenceBreakdown = new ConfidenceLevelStats
            {
                HighConfidence = recentMetrics.Count(m => m.Confidence > 0.8),
                MediumConfidence = recentMetrics.Count(m => m.Confidence >= 0.5 && m.Confidence <= 0.8),
                LowConfidence = recentMetrics.Count(m => m.Confidence < 0.5)
            };

            var stats = new PredictionPerformanceStats
            {
                TotalPredictions = _predictionStats["total"],
                SuccessfulPredictions = _predictionStats["successful"],
                AveragePredictionTime = avgPredictionTime,
                AverageConfidence = avgConfidence,
                ConfidenceBreakdown = confidenceBreakdown,
                StatsPeriod = TimeSpan.FromHours(1) // Stats cover last hour of activity
            };

            return Task.FromResult(Result<PredictionPerformanceStats>.Success(stats));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get performance statistics");
            return Task.FromResult(Result<PredictionPerformanceStats>.Failure($"Statistics error: {ex.Message}"));
        }
    }

    // Private helper methods

    private SimplePredictionResult MakePrediction(string filename, TokenizedFilename tokenized, FeatureVector features)
    {
        // Simple pattern-based prediction using series tokens
        var seriesTokens = tokenized.SeriesTokens.Select(t => t.ToUpperInvariant()).ToList();
        var joinedTokens = string.Join(" ", seriesTokens);

        // Italian content series mapping based on common patterns
        var italianSeries = new Dictionary<string, double>
        {
            { "IL TRONO DI SPADE", CalculateSeriesConfidence(seriesTokens, new[] { "IL", "TRONO", "DI", "SPADE", "GAME", "THRONES" }) },
            { "ONE PIECE", CalculateSeriesConfidence(seriesTokens, new[] { "ONE", "PIECE" }) },
            { "MY HERO ACADEMIA", CalculateSeriesConfidence(seriesTokens, new[] { "MY", "HERO", "ACADEMIA", "BOKU", "NO" }) },
            { "NARUTO", CalculateSeriesConfidence(seriesTokens, new[] { "NARUTO", "BORUTO" }) },
            { "BREAKING BAD", CalculateSeriesConfidence(seriesTokens, new[] { "BREAKING", "BAD" }) },
            { "THE WALKING DEAD", CalculateSeriesConfidence(seriesTokens, new[] { "THE", "WALKING", "DEAD" }) },
            { "STRANGER THINGS", CalculateSeriesConfidence(seriesTokens, new[] { "STRANGER", "THINGS" }) },
            { "CASA DI CARTA", CalculateSeriesConfidence(seriesTokens, new[] { "LA", "CASA", "DE", "PAPEL", "MONEY", "HEIST" }) }
        };

        // Find best match
        var bestMatch = italianSeries.OrderByDescending(kvp => kvp.Value).First();
        var confidence = bestMatch.Value;

        // Apply Italian content boost if detected
        var italianIndicators = AnalyzeItalianContent(filename);
        if (italianIndicators.ItalianConfidence > 0.5)
        {
            confidence = Math.Min(1.0, confidence * 1.2); // 20% boost for Italian content
        }

        // Generate alternatives
        var alternatives = italianSeries
            .Where(kvp => kvp.Key != bestMatch.Key && kvp.Value > 0.1)
            .OrderByDescending(kvp => kvp.Value)
            .Take(3)
            .Select(kvp => new AlternativeCategory { Category = kvp.Key, Confidence = kvp.Value * 0.8 })
            .ToList();

        return new SimplePredictionResult
        {
            PredictedCategory = bestMatch.Key,
            Confidence = confidence,
            AlternativeCategories = alternatives.AsReadOnly()
        };
    }

    private double CalculateSeriesConfidence(List<string> tokens, string[] patterns)
    {
        var matches = patterns.Count(pattern => tokens.Any(token => 
            token.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
            pattern.Contains(token, StringComparison.OrdinalIgnoreCase)));
            
        return Math.Min(1.0, (double)matches / patterns.Length);
    }

    private ClassificationResult CreateClassificationResult(
        string filename, 
        TokenizedFilename tokenized, 
        SimplePredictionResult prediction, 
        TimeSpan processingTime)
    {
        // Determine classification decision based on configuration thresholds
        ClassificationDecision decision;
        if (prediction.Confidence >= _config.AutoClassifyThreshold)
        {
            decision = ClassificationDecision.AutoClassify;
        }
        else if (prediction.Confidence >= _config.SuggestionThreshold)
        {
            decision = ClassificationDecision.SuggestWithAlternatives;
        }
        else if (prediction.Confidence >= _config.ManualCategorizationThreshold)
        {
            decision = ClassificationDecision.RequestManualCategorization;
        }
        else
        {
            decision = ClassificationDecision.Unreliable;
        }

        return new ClassificationResult
        {
            Filename = filename,
            PredictedCategory = prediction.PredictedCategory,
            Confidence = (float)prediction.Confidence,
            Decision = decision,
            ProcessingTimeMs = (long)processingTime.TotalMilliseconds,
            AlternativePredictions = prediction.AlternativeCategories.Select(alt => 
                new CategoryPrediction
                {
                    Category = alt.Category,
                    Confidence = (float)alt.Confidence
                }).ToList().AsReadOnly(),
            ModelVersion = "1.0.0-pattern-based"
        };
    }

    private FilenameComplexity AnalyzeFilenameComplexity(string filename)
    {
        var nameWithoutExt = Path.GetFileNameWithoutExtension(filename);
        var tokens = nameWithoutExt.Split(new[] { '.', '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var separatorCount = nameWithoutExt.Count(c => c == '.' || c == '_' || c == '-' || c == ' ');

        var detectedPatterns = new List<string>();
        var hasSpecialPatterns = false;

        // Check for episode patterns
        if (tokens.Any(t => System.Text.RegularExpressions.Regex.IsMatch(t, @"[Ss]\d+[Ee]\d+")))
        {
            detectedPatterns.Add("SeasonEpisode");
            hasSpecialPatterns = true;
        }

        // Check for quality patterns
        if (tokens.Any(t => new[] { "720p", "1080p", "4K", "2160p" }.Contains(t, StringComparer.OrdinalIgnoreCase)))
        {
            detectedPatterns.Add("Quality");
            hasSpecialPatterns = true;
        }

        // Check for language patterns
        if (tokens.Any(t => new[] { "ITA", "ENG", "SUB", "DUB" }.Contains(t, StringComparer.OrdinalIgnoreCase)))
        {
            detectedPatterns.Add("Language");
            hasSpecialPatterns = true;
        }

        var complexityScore = Math.Min(10, (tokens.Length / 2) + (separatorCount / 3) + (hasSpecialPatterns ? 2 : 0));

        return new FilenameComplexity
        {
            ComplexityScore = complexityScore,
            TokenCount = tokens.Length,
            SeparatorCount = separatorCount,
            HasSpecialPatterns = hasSpecialPatterns,
            DetectedPatterns = detectedPatterns.AsReadOnly()
        };
    }

    private ItalianContentIndicators AnalyzeItalianContent(string filename)
    {
        var upperFilename = filename.ToUpperInvariant();
        
        var hasItalianLanguage = upperFilename.Contains("ITA") || upperFilename.Contains("ITALIAN");
        
        var italianReleaseGroups = new[] { "NOVARIP", "DARKSIDEMUX", "PIR8", "MEM", "UBI" };
        var italianReleaseGroup = italianReleaseGroups.FirstOrDefault(group => upperFilename.Contains(group));
        var hasItalianReleaseGroup = italianReleaseGroup != null;

        var italianSeriesPatterns = new[] { "TRONO", "SPADE", "CASA", "CARTA", "HERO", "ACADEMIA" };
        var hasItalianSeries = italianSeriesPatterns.Any(pattern => upperFilename.Contains(pattern));

        // Calculate Italian confidence based on indicators
        var confidence = 0.0;
        if (hasItalianLanguage) confidence += 0.4;
        if (hasItalianReleaseGroup) confidence += 0.4;
        if (hasItalianSeries) confidence += 0.2;

        return new ItalianContentIndicators
        {
            HasItalianLanguage = hasItalianLanguage,
            HasItalianReleaseGroup = hasItalianReleaseGroup,
            ItalianReleaseGroup = italianReleaseGroup,
            HasItalianSeries = hasItalianSeries,
            ItalianConfidence = confidence
        };
    }

    private double CalculateProcessingConfidence(
        string filename, 
        FilenameComplexity complexity, 
        ItalianContentIndicators italianIndicators,
        List<ValidationIssue> issues)
    {
        var confidence = 0.8; // Base confidence

        // Adjust for complexity
        confidence -= Math.Max(0, (complexity.ComplexityScore - 5) * 0.05);

        // Boost for Italian content (this service is optimized for it)
        confidence += italianIndicators.ItalianConfidence * 0.1;

        // Reduce for validation issues
        confidence -= issues.Count(i => i.Severity == IssueSeverity.Error) * 0.2;
        confidence -= issues.Count(i => i.Severity == IssueSeverity.Warning) * 0.1;

        return Math.Max(0.0, Math.Min(1.0, confidence));
    }

    private void RecordPredictionMetric(ClassificationResult result, TimeSpan duration)
    {
        var metric = new PredictionMetric
        {
            Timestamp = DateTime.UtcNow,
            Filename = result.Filename,
            Category = result.PredictedCategory,
            Confidence = result.Confidence,
            Duration = duration,
            Success = result.Decision != ClassificationDecision.Failed
        };

        _recentPredictions.Enqueue(metric);

        // Keep only recent metrics to prevent memory growth
        while (_recentPredictions.Count > MaxRecentPredictions)
        {
            _recentPredictions.TryDequeue(out _);
        }
    }

    private List<PredictionMetric> GetRecentMetrics()
    {
        return _recentPredictions.ToList();
    }

    private sealed record PredictionMetric
    {
        public required DateTime Timestamp { get; init; }
        public required string Filename { get; init; }
        public required string Category { get; init; }
        public required double Confidence { get; init; }
        public required TimeSpan Duration { get; init; }
        public required bool Success { get; init; }
    }

    private sealed record SimplePredictionResult
    {
        public required string PredictedCategory { get; init; }
        public required double Confidence { get; init; }
        public required IReadOnlyList<AlternativeCategory> AlternativeCategories { get; init; }
    }

    private sealed record AlternativeCategory
    {
        public required string Category { get; init; }
        public required double Confidence { get; init; }
    }
}