using MediaButler.Core.Common;
using MediaButler.ML.Configuration;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;
using MediaButler.ML.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML;
using Microsoft.ML.Data;
using System.Diagnostics;

namespace MediaButler.ML.Services;

/// <summary>
/// ML.NET-powered file classification service for Italian TV series.
/// Orchestrates tokenization, feature extraction, and ML prediction using trained model.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Orchestrates ML classification pipeline
/// - Compose don't complect: Uses independent services (Tokenizer, Features, Prediction)
/// - Values over state: Immutable classification results
/// - Simple artifacts: Clear prediction pipeline flow
///
/// Pipeline: Filename → Tokenize → Extract Features → Predict → Format Result
/// </remarks>
public class FastTextClassificationService : IClassificationService
{
    private readonly ITokenizerService _tokenizer;
    private readonly IFeatureEngineeringService _featureService;
    private readonly IPredictionService _predictionService;
    private readonly MLConfiguration _config;
    private readonly ILogger<FastTextClassificationService> _logger;

    private PredictionEngine<SeriesFeatureInput, SeriesPrediction>? _predictionEngine;
    private ITransformer? _trainedModel;
    private MLContext? _mlContext;
    private bool _modelLoaded;
    private readonly object _modelLock = new();
    private ModelInfo? _modelInfo;
    private IReadOnlyList<string>? _availableCategories;

    // Prediction caching (LRU cache for repeated classifications)
    private readonly LruCache<string, ClassificationResult>? _predictionCache;
    private readonly bool _cachingEnabled;

    public FastTextClassificationService(
        ITokenizerService tokenizer,
        IFeatureEngineeringService featureService,
        IPredictionService predictionService,
        IOptions<MLConfiguration> config,
        ILogger<FastTextClassificationService> logger)
    {
        _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
        _featureService = featureService ?? throw new ArgumentNullException(nameof(featureService));
        _predictionService = predictionService ?? throw new ArgumentNullException(nameof(predictionService));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize prediction cache if enabled
        _cachingEnabled = _config.Features.EnablePredictionCaching;
        if (_cachingEnabled)
        {
            _predictionCache = new LruCache<string, ClassificationResult>(_config.Cache.MaxCacheSize);
            _logger.LogInformation("Prediction caching enabled with capacity: {Capacity}", _config.Cache.MaxCacheSize);
        }
        else
        {
            _logger.LogInformation("Prediction caching disabled");
        }
    }

    /// <summary>
    /// Classifies a filename into a category using the trained ML.NET model.
    /// </summary>
    public async Task<Result<ClassificationResult>> ClassifyFilenameAsync(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return Result<ClassificationResult>.Failure("Filename cannot be null or empty");
        }

        // Check cache first if enabled
        if (_cachingEnabled && _predictionCache != null)
        {
            if (_predictionCache.TryGet(filename, out var cachedResult) && cachedResult != null)
            {
                _logger.LogDebug("Cache hit for filename: {Filename}", filename);
                return Result<ClassificationResult>.Success(cachedResult);
            }
        }

        // Ensure model is loaded
        if (!_modelLoaded)
        {
            var loadResult = await LoadModelAsync();
            if (loadResult.IsFailure)
            {
                return Result<ClassificationResult>.Failure($"Model not loaded: {loadResult.Error}");
            }
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Step 1: Tokenize filename
            var tokenResult = _tokenizer.TokenizeFilename(filename);
            if (tokenResult.IsFailure)
            {
                return Result<ClassificationResult>.Failure($"Tokenization failed: {tokenResult.Error}");
            }

            // Step 2: Extract features
            var featureResult = _featureService.ExtractFeatures(tokenResult.Value);
            if (featureResult.IsFailure)
            {
                return Result<ClassificationResult>.Failure($"Feature extraction failed: {featureResult.Error}");
            }

            // Step 3: Prepare ML.NET input (must match training schema)
            var mlInput = new SeriesFeatureInput
            {
                Filename = filename,
                SeriesName = ExtractSeriesName(filename), // NEW: Extract series name for better classification
                Category = string.Empty, // Required by ML.NET schema but not used during prediction
                Confidence = 0.0f, // Required by ML.NET schema but not used during prediction
                Source = "Prediction", // Required by ML.NET schema but not used during prediction
                // QualityTier = ExtractQualityTier(filename),
                // VideoCodec = ExtractVideoCodec(filename)
            };

            // DEBUG: Log input features
            _logger.LogInformation(
                "ML Input - Filename: {Filename}, SeriesName: '{SeriesName}'",
                mlInput.Filename, mlInput.SeriesName);

            // Step 4: Predict using ML.NET
            SeriesPrediction prediction;
            lock (_modelLock)
            {
                if (_predictionEngine == null)
                {
                    return Result<ClassificationResult>.Failure("Prediction engine not initialized");
                }
                prediction = _predictionEngine.Predict(mlInput);
            }

            // DEBUG: Log prediction results
            _logger.LogInformation(
                "ML Prediction - LabelIndex: {LabelIndex}, TopScore: {Score:P2}, ScoresCount: {Count}",
                prediction.PredictedLabelIndex, prediction.Score.Max(), prediction.Score.Length);

            stopwatch.Stop();

            // Step 5: Map predicted label index to category name
            var predictedCategory = MapLabelIndexToCategory(prediction.PredictedLabelIndex, prediction.Score);

            // Step 6: Format result
            var classificationResult = BuildClassificationResult(
                filename,
                predictedCategory,
                prediction.Score,
                featureResult.Value,
                stopwatch.ElapsedMilliseconds);

            // Record metrics (removed - method doesn't exist in IPredictionService)
            // The PredictionService handles this internally

            _logger.LogInformation(
                "Classified {Filename} as {Category} with {Confidence:P2} confidence in {Duration}ms",
                filename, classificationResult.PredictedCategory, classificationResult.Confidence, stopwatch.ElapsedMilliseconds);

            // Cache the result if successful and caching enabled
            if (_cachingEnabled && _predictionCache != null && classificationResult.Decision != ClassificationDecision.Failed)
            {
                _predictionCache.Set(filename, classificationResult);
                _logger.LogDebug("Cached classification result for: {Filename}", filename);
            }

            return Result<ClassificationResult>.Success(classificationResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying filename: {Filename}", filename);
            return Result<ClassificationResult>.Failure($"Classification error: {ex.Message}");
        }
    }

    /// <summary>
    /// Classifies multiple filenames in batch for improved performance.
    /// </summary>
    public async Task<Result<IEnumerable<ClassificationResult>>> ClassifyBatchAsync(IEnumerable<string> filenames)
    {
        if (filenames == null)
        {
            return Result<IEnumerable<ClassificationResult>>.Failure("Filenames collection cannot be null");
        }

        var filenameList = filenames.ToList();
        if (filenameList.Count == 0)
        {
            return Result<IEnumerable<ClassificationResult>>.Success(Enumerable.Empty<ClassificationResult>());
        }

        _logger.LogInformation("Classifying batch of {Count} filenames", filenameList.Count);

        var results = new List<ClassificationResult>();
        var errors = new List<string>();

        foreach (var filename in filenameList)
        {
            var result = await ClassifyFilenameAsync(filename);
            if (result.IsSuccess)
            {
                results.Add(result.Value);
            }
            else
            {
                errors.Add($"{filename}: {result.Error}");
                _logger.LogWarning("Failed to classify {Filename}: {Error}", filename, result.Error);
            }
        }

        if (errors.Any())
        {
            _logger.LogWarning("Batch classification completed with {ErrorCount} errors out of {Total}",
                errors.Count, filenameList.Count);
        }

        return Result<IEnumerable<ClassificationResult>>.Success(results);
    }

    /// <summary>
    /// Gets available categories from the trained model.
    /// </summary>
    public Result<IEnumerable<string>> GetAvailableCategories()
    {
        if (!_modelLoaded || _availableCategories == null)
        {
            return Result<IEnumerable<string>>.Failure("Model not loaded - categories unavailable");
        }

        return Result<IEnumerable<string>>.Success(_availableCategories);
    }

    /// <summary>
    /// Gets model information and performance metrics.
    /// </summary>
    public Result<ModelInfo> GetModelInfo()
    {
        if (!_modelLoaded || _modelInfo == null)
        {
            return Result<ModelInfo>.Failure("Model not loaded - info unavailable");
        }

        // Add cache statistics to metadata if caching is enabled
        var metadata = new Dictionary<string, object>(_modelInfo.Metadata);
        if (_cachingEnabled && _predictionCache != null)
        {
            var cacheStats = _predictionCache.GetStatistics();
            metadata["CacheEnabled"] = true;
            metadata["CacheCapacity"] = cacheStats.Capacity;
            metadata["CacheCount"] = cacheStats.Count;
            metadata["CacheHits"] = cacheStats.Hits;
            metadata["CacheMisses"] = cacheStats.Misses;
            metadata["CacheHitRate"] = cacheStats.HitRate;
        }
        else
        {
            metadata["CacheEnabled"] = false;
        }

        var modelInfoWithCache = _modelInfo with { Metadata = metadata };
        return Result<ModelInfo>.Success(modelInfoWithCache);
    }

    /// <summary>
    /// Checks if the ML model is loaded and ready for predictions.
    /// </summary>
    public bool IsModelReady()
    {
        return _modelLoaded && _predictionEngine != null;
    }

    /// <summary>
    /// Loads the trained ML.NET model from disk (lazy initialization).
    /// </summary>
    private Task<Result<bool>> LoadModelAsync()
    {
        lock (_modelLock)
        {
            if (_modelLoaded)
            {
                return Task.FromResult(Result<bool>.Success(true));
            }

            try
            {
                _logger.LogInformation("Loading ML.NET model from {ModelPath}", _config.ModelPath);

                var modelPath = Path.Combine(_config.ModelPath, "classification-simplified-model.zip");

                if (!File.Exists(modelPath))
                {
                    _logger.LogWarning("Model file not found at {ModelPath} - model needs to be trained first", modelPath);
                    return Task.FromResult(Result<bool>.Failure($"Model file not found: {modelPath}"));
                }

                _mlContext = new MLContext(seed: 42);

                // Load the trained model
                _trainedModel = _mlContext.Model.Load(modelPath, out var modelInputSchema);

                // Extract category labels from model schema BEFORE creating prediction engine
                ExtractCategoryLabelsFromModel(_trainedModel, modelInputSchema);

                // Create prediction engine
                _predictionEngine = _mlContext.Model.CreatePredictionEngine<SeriesFeatureInput, SeriesPrediction>(_trainedModel);

                // Extract model metadata
                ExtractModelMetadata(modelPath);

                _modelLoaded = true;

                _logger.LogInformation(
                    "Model loaded successfully: {Version}, {CategoryCount} categories, trained at {TrainedAt}",
                    _modelInfo?.Version, _modelInfo?.CategoryCount, _modelInfo?.TrainedAt);

                return Task.FromResult(Result<bool>.Success(true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load ML.NET model");
                return Task.FromResult(Result<bool>.Failure($"Model loading error: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// Extracts category labels from the trained model's schema.
    /// The model's Label column contains the key-to-value mapping for categories.
    /// </summary>
    private void ExtractCategoryLabelsFromModel(ITransformer model, DataViewSchema schema)
    {
        try
        {
            // Find the PredictedLabel column in the output schema
            var outputSchema = model.GetOutputSchema(schema);

            // Iterate through columns to find PredictedLabel
            foreach (var column in outputSchema)
            {
                if (column.Name == "PredictedLabel" && column.Type is KeyDataViewType keyType)
                {
                    // Get the key values metadata (category names)
                    var keyValues = default(VBuffer<ReadOnlyMemory<char>>);
                    column.Annotations.GetValue("KeyValues", ref keyValues);

                    if (keyValues.Length > 0)
                    {
                        var categories = new List<string>();
                        foreach (var value in keyValues.GetValues())
                        {
                            categories.Add(value.ToString());
                        }

                        _availableCategories = categories.AsReadOnly();
                        _logger.LogInformation("Extracted {Count} categories from model: {Categories}",
                            categories.Count, string.Join(", ", categories.Take(5)) + "...");
                        return;
                    }
                }
            }

            _logger.LogWarning("Could not extract category labels from model schema, using fallback list");
            UseFallbackCategories();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting category labels from model, using fallback list");
            UseFallbackCategories();
        }
    }

    /// <summary>
    /// Uses fallback hardcoded categories if extraction from model fails.
    /// </summary>
    private void UseFallbackCategories()
    {
        _availableCategories = new List<string>
        {
            "ARROW", "ATTACK ON TITAN", "BETTER CALL SAUL", "BREAKING BAD",
            "DEMON SLAYER", "GAME OF THRONES", "LUCIFER", "MONEY HEIST",
            "MY HERO ACADEMIA", "NARUTO SHIPPUDEN", "ONE PIECE",
            "PEAKY BLINDERS", "STRANGER THINGS", "THE BOYS", "THE CROWN",
            "THE FLASH", "THE MANDALORIAN", "THE UMBRELLA ACADEMY",
            "THE WALKING DEAD", "THE WITCHER", "VIKINGS", "WESTWORLD"
        }.AsReadOnly();
    }

    /// <summary>
    /// Extracts model metadata and available categories from the model file.
    /// </summary>
    private void ExtractModelMetadata(string modelPath)
    {
        // For now, use static metadata - in production this would be loaded from model metadata
        _modelInfo = new ModelInfo
        {
            Version = _config.ActiveModelVersion,
            Algorithm = "ML.NET SDCA Maximum Entropy",
            TrainedAt = File.GetLastWriteTimeUtc(modelPath),
            TestAccuracy = 0.95f, // TODO: Load from saved model metadata
            CategoryCount = _availableCategories?.Count ?? 0,
            TrainingSamples = 114, // TODO: Extract from model metadata
            TestPrecision = 0.93f,
            TestRecall = 0.91f,
            TestF1Score = 0.92f,
            FileSizeBytes = new FileInfo(modelPath).Length,
            AverageInferenceTimeMs = 50.0 // Will be updated with actual predictions
        };
    }

    /// <summary>
    /// Maps the predicted label index to the actual category name using available categories.
    /// </summary>
    private string MapLabelIndexToCategory(uint labelIndex, float[] scores)
    {
        if (_availableCategories == null || labelIndex >= _availableCategories.Count)
        {
            // Fallback: find the index of the highest score
            var maxIndex = Array.IndexOf(scores, scores.Max());
            if (_availableCategories != null && maxIndex >= 0 && maxIndex < _availableCategories.Count)
            {
                return _availableCategories[maxIndex];
            }
            return "UNKNOWN";
        }

        return _availableCategories[(int)labelIndex];
    }

    /// <summary>
    /// Builds a ClassificationResult from ML.NET prediction and extracted features.
    /// </summary>
    private ClassificationResult BuildClassificationResult(
        string filename,
        string predictedCategory,
        float[] scores,
        FeatureVector features,
        long processingTimeMs)
    {
        var confidence = scores.Max();

        // Get top 3 alternative predictions
        var alternatives = scores
            .Select((score, index) => new {
                Score = score,
                Category = _availableCategories != null && index < _availableCategories.Count
                    ? _availableCategories[index]
                    : $"Category_{index}"
            })
            .OrderByDescending(x => x.Score)
            .Skip(1) // Skip the top prediction
            .Take(3)
            .Select(x => new CategoryPrediction
            {
                Category = x.Category,
                Confidence = x.Score
            })
            .ToArray();

        // Determine classification decision based on confidence thresholds
        var decision = DetermineDecision(confidence);

        return new ClassificationResult
        {
            Filename = filename,
            PredictedCategory = predictedCategory,
            Confidence = confidence,
            AlternativePredictions = alternatives,
            Decision = decision,
            Features = new Dictionary<string, object>
            {
                ["FeatureCount"] = features.FeatureCount,
                ["TokenFeatureCount"] = features.TokenFeatures.FeatureCount,
                ["HasEpisode"] = features.EpisodeFeatures != null,
                // ["Quality"] = features.QualityFeatures.ToString() ?? "Unknown",
                ["NGramCount"] = features.NGramFeatures.Count
            },
            ModelVersion = _modelInfo?.Version ?? 0,
            ClassifiedAt = DateTime.UtcNow,
            ProcessingTimeMs = processingTimeMs
        };
    }

    /// <summary>
    /// Determines classification decision based on confidence threshold.
    /// </summary>
    private ClassificationDecision DetermineDecision(float confidence)
    {
        if (confidence >= _config.AutoClassifyThreshold)
        {
            return ClassificationDecision.AutoClassify;
        }

        if (confidence >= _config.SuggestionThreshold)
        {
            return ClassificationDecision.SuggestWithAlternatives;
        }

        return ClassificationDecision.Failed;
    }

    /// <summary>
    /// Extracts quality tier from filename (must match training logic).
    /// </summary>
    private string ExtractQualityTier(string filename)
    {
        if (filename.Contains("2160p") || filename.Contains("4K"))
            return "Ultra";
        if (filename.Contains("1080p"))
            return "High";
        if (filename.Contains("720p"))
            return "Medium";
        return "Standard";
    }

    /// <summary>
    /// Extracts video codec from filename (must match training logic).
    /// </summary>
    private string ExtractVideoCodec(string filename)
    {
        if (filename.Contains("x265") || filename.Contains("HEVC"))
            return "HEVC";
        if (filename.Contains("x264") || filename.Contains("AVC"))
            return "AVC";
        return "Unknown";
    }

    /// <summary>
    /// Extracts the series name from a filename by identifying tokens before episode markers.
    /// This helps focus classification on the actual series name rather than release metadata.
    /// Must match the exact logic used in ModelTrainingService.
    /// </summary>
    /// <param name="filename">The filename to extract series name from</param>
    /// <returns>The extracted series name (cleaned and trimmed)</returns>
    private string ExtractSeriesName(string filename)
    {
        // Normalize separators: dots and underscores to spaces
        var normalized = filename
            .Replace('.', ' ')
            .Replace('_', ' ');

        // Episode marker patterns (ordered by specificity)
        var episodePatterns = new[]
        {
            @"\s+S\d{1,2}E\d{1,2}",        // S01E01, S1E1
            @"\s+\d{1,2}x\d{1,2}",         // 1x01, 21x3
            @"\s+Season\s+\d+",            // Season 1
            @"\s+Episode\s+\d+",           // Episode 1
            @"\s+\d{4}\s",                 // Year like 2024 (followed by space)
            @"\s+\d{1,4}\s+(ITA|ENG|SUB)", // Episode number before language
        };

        // Find the earliest episode marker
        int earliestIndex = normalized.Length;
        foreach (var pattern in episodePatterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(normalized, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && match.Index < earliestIndex)
            {
                earliestIndex = match.Index;
            }
        }

        // Extract everything before the episode marker
        var seriesName = normalized.Substring(0, earliestIndex).Trim();

        // Remove common file extensions if present
        seriesName = System.Text.RegularExpressions.Regex.Replace(seriesName,
            @"\.(mkv|mp4|avi)$", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Clean up multiple spaces
        seriesName = System.Text.RegularExpressions.Regex.Replace(seriesName, @"\s+", " ");

        // Return cleaned series name or fallback to first 3 words
        if (string.IsNullOrWhiteSpace(seriesName))
        {
            var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            seriesName = string.Join(" ", words.Take(3));
        }

        return seriesName.Trim();
    }
}

/// <summary>
/// Input features for ML.NET prediction (must match exact training schema).
/// All columns from training must be present, even if not used during prediction.
/// </summary>
public class SeriesFeatureInput
{
    /// <summary>
    /// The filename to classify (kept for compatibility).
    /// </summary>
    public string Filename { get; set; } = string.Empty;

    /// <summary>
    /// The series name extracted from filename (primary feature for classification).
    /// This is the key feature that focuses on series-identifying tokens.
    /// </summary>
    public string SeriesName { get; set; } = string.Empty;

    /// <summary>
    /// Category label (required by ML.NET schema, not used during prediction).
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score (required by ML.NET schema, not used during prediction).
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// Source identifier (required by ML.NET schema, not used during prediction).
    /// </summary>
    public string Source { get; set; } = string.Empty;

    // /// <summary>
    // /// Quality tier extracted from filename (Ultra/High/Medium/Standard).
    // /// </summary>
    // public string QualityTier { get; set; } = string.Empty;
    //
    // /// <summary>
    // /// Video codec extracted from filename (HEVC/AVC/Unknown).
    // /// </summary>
    //  public string VideoCodec { get; set; } = string.Empty;
}

/// <summary>
/// ML.NET prediction output.
/// The PredictedLabel is a Key type (uint) that represents the index of the predicted category.
/// </summary>
public class SeriesPrediction
{
    [Microsoft.ML.Data.ColumnName("PredictedLabel")]
    public uint PredictedLabelIndex { get; set; }

    [Microsoft.ML.Data.ColumnName("Score")]
    public float[] Score { get; set; } = Array.Empty<float>();
}
