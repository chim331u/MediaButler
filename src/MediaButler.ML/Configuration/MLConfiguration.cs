namespace MediaButler.ML.Configuration;

/// <summary>
/// Configuration settings for the ML classification system.
/// This class holds all ML-related configuration in one place following "Simple Made Easy" principles.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Only holds ML configuration
/// - No complecting: Separate from domain and API configuration
/// - Values over state: Configuration as immutable data
/// - Declarative: Describes what ML should do, not how
/// </remarks>
public class MLConfiguration
{
    /// <summary>
    /// Gets or sets the path where ML models are stored.
    /// </summary>
    /// <example>"/app/models" or "C:\MediaButler\models"</example>
    public string ModelPath { get; set; } = "models";

    /// <summary>
    /// Gets or sets the active model version to use for predictions.
    /// </summary>
    /// <example>"1.0.0", "2.1.3"</example>
    public string ActiveModelVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the confidence threshold for auto-classification.
    /// Files with confidence above this threshold are auto-classified pending confirmation.
    /// </summary>
    /// <remarks>
    /// Default: 0.85 (85% confidence)
    /// Range: 0.0 to 1.0
    /// </remarks>
    public float AutoClassifyThreshold { get; set; } = 0.85f;

    /// <summary>
    /// Gets or sets the confidence threshold for suggesting alternatives.
    /// Files with confidence between this and AutoClassifyThreshold get alternative suggestions.
    /// </summary>
    /// <remarks>
    /// Default: 0.50 (50% confidence)
    /// Range: 0.0 to AutoClassifyThreshold
    /// </remarks>
    public float SuggestionThreshold { get; set; } = 0.50f;

    /// <summary>
    /// Gets or sets the confidence threshold for requesting manual categorization.
    /// Files with confidence between this and SuggestionThreshold require manual categorization.
    /// </summary>
    /// <remarks>
    /// Default: 0.25 (25% confidence)
    /// Range: 0.0 to SuggestionThreshold
    /// Files below this threshold are considered unreliable.
    /// </remarks>
    public float ManualCategorizationThreshold { get; set; } = 0.25f;

    /// <summary>
    /// Gets or sets the maximum time allowed for a single classification in milliseconds.
    /// </summary>
    /// <remarks>
    /// Default: 500ms (ARM32 optimized)
    /// This timeout ensures responsive performance on low-power devices.
    /// </remarks>
    public int MaxClassificationTimeMs { get; set; } = 500;

    /// <summary>
    /// Gets or sets the maximum number of alternative predictions to return.
    /// </summary>
    /// <remarks>
    /// Default: 3 alternatives
    /// Higher values provide more options but may confuse users.
    /// </remarks>
    public int MaxAlternativePredictions { get; set; } = 3;

    /// <summary>
    /// Gets or sets whether to enable batch processing for multiple files.
    /// </summary>
    /// <remarks>
    /// Default: true
    /// Batch processing improves performance but uses more memory.
    /// </remarks>
    public bool EnableBatchProcessing { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum batch size for processing multiple files.
    /// </summary>
    /// <remarks>
    /// Default: 50 files per batch (ARM32 optimized)
    /// Larger batches improve performance but increase memory usage.
    /// </remarks>
    public int MaxBatchSize { get; set; } = 50;

    /// <summary>
    /// Gets or sets whether to automatically retrain the model based on user feedback.
    /// </summary>
    /// <remarks>
    /// Default: true
    /// Auto-retraining improves accuracy over time but requires computational resources.
    /// </remarks>
    public bool EnableAutoRetraining { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum number of new samples required before triggering retraining.
    /// </summary>
    /// <remarks>
    /// Default: 100 new samples
    /// Higher values reduce retraining frequency but may delay accuracy improvements.
    /// </remarks>
    public int RetrainingThreshold { get; set; } = 100;

    /// <summary>
    /// Gets or sets the tokenization configuration.
    /// </summary>
    public TokenizationConfiguration Tokenization { get; set; } = new();

    /// <summary>
    /// Gets or sets the training configuration.
    /// </summary>
    public TrainingConfiguration Training { get; set; } = new();

    /// <summary>
    /// Gets or sets additional ML features to enable/disable.
    /// </summary>
    public MLFeatureFlags Features { get; set; } = new();

    /// <summary>
    /// Gets or sets the CSV import configuration for training data.
    /// </summary>
    public CsvImportSettings CsvImport { get; set; } = new();
}

/// <summary>
/// Configuration for filename tokenization.
/// </summary>
public class TokenizationConfiguration
{
    /// <summary>
    /// Gets or sets whether to normalize separators (dots, underscores to spaces).
    /// </summary>
    public bool NormalizeSeparators { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to remove common quality indicators during tokenization.
    /// </summary>
    /// <example>1080p, 720p, BluRay, HDTV, etc.</example>
    public bool RemoveQualityIndicators { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to remove language codes during tokenization.
    /// </summary>
    /// <example>ENG, ITA, FRA, SUB, DUB, etc.</example>
    public bool RemoveLanguageCodes { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to remove release tags during tokenization.
    /// </summary>
    /// <example>FINAL, REPACK, PROPER, etc.</example>
    public bool RemoveReleaseTags { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to convert all tokens to lowercase for consistency.
    /// </summary>
    public bool ConvertToLowercase { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum token length to keep.
    /// </summary>
    /// <remarks>
    /// Default: 2 characters
    /// Shorter tokens are typically not meaningful for classification.
    /// </remarks>
    public int MinTokenLength { get; set; } = 2;

    /// <summary>
    /// Gets or sets custom patterns to remove during tokenization.
    /// </summary>
    public List<string> CustomRemovalPatterns { get; set; } = new();
}

/// <summary>
/// Configuration for ML model training.
/// </summary>
public class TrainingConfiguration
{
    /// <summary>
    /// Gets or sets the ratio of data to use for training.
    /// </summary>
    /// <remarks>
    /// Default: 0.7 (70% for training)
    /// Range: 0.1 to 0.8
    /// </remarks>
    public double TrainingRatio { get; set; } = 0.7;

    /// <summary>
    /// Gets or sets the ratio of data to use for validation.
    /// </summary>
    /// <remarks>
    /// Default: 0.2 (20% for validation)
    /// Range: 0.1 to 0.4
    /// </remarks>
    public double ValidationRatio { get; set; } = 0.2;

    /// <summary>
    /// Gets or sets the number of training iterations.
    /// </summary>
    /// <remarks>
    /// Default: 100 iterations (ARM32 optimized)
    /// More iterations may improve accuracy but increase training time.
    /// </remarks>
    public int NumberOfIterations { get; set; } = 100;

    /// <summary>
    /// Gets or sets the learning rate for training.
    /// </summary>
    /// <remarks>
    /// Default: 0.1
    /// Lower values provide more stable training but may be slower to converge.
    /// </remarks>
    public double LearningRate { get; set; } = 0.1;

    /// <summary>
    /// Gets or sets whether to use early stopping during training.
    /// </summary>
    /// <remarks>
    /// Default: true
    /// Early stopping prevents overfitting and reduces training time.
    /// </remarks>
    public bool UseEarlystopping { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum accuracy required for a model to be considered valid.
    /// </summary>
    /// <remarks>
    /// Default: 0.75 (75% accuracy)
    /// Models below this threshold are not deployed.
    /// </remarks>
    public float MinimumAccuracy { get; set; } = 0.75f;
}

/// <summary>
/// Feature flags for enabling/disabling ML functionality.
/// </summary>
public class MLFeatureFlags
{
    /// <summary>
    /// Gets or sets whether to use episode information as features.
    /// </summary>
    public bool UseEpisodeFeatures { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to use quality information as features.
    /// </summary>
    public bool UseQualityFeatures { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to use file extension as a feature.
    /// </summary>
    public bool UseFileExtensionFeature { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable detailed performance logging.
    /// </summary>
    /// <remarks>
    /// Default: false (disabled for ARM32 performance)
    /// Detailed logging provides insights but impacts performance.
    /// </remarks>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to cache model predictions for identical filenames.
    /// </summary>
    /// <remarks>
    /// Default: true
    /// Caching improves performance for repeated predictions.
    /// </remarks>
    public bool EnablePredictionCaching { get; set; } = true;
}

/// <summary>
/// Configuration for CSV training data import.
/// </summary>
public class CsvImportSettings
{
    /// <summary>
    /// Gets or sets the default path for training data CSV files.
    /// </summary>
    /// <example>"data/training_data.csv"</example>
    public string DefaultCsvPath { get; set; } = "data/training_data.csv";

    /// <summary>
    /// Gets or sets the CSV separator character.
    /// </summary>
    /// <remarks>Default: ';' (semicolon) as per specification</remarks>
    public char Separator { get; set; } = ';';

    /// <summary>
    /// Gets or sets whether to normalize category names to uppercase.
    /// </summary>
    /// <remarks>Default: true (consistent with MediaButler conventions)</remarks>
    public bool NormalizeCategoryNames { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to skip duplicate filenames during import.
    /// </summary>
    /// <remarks>Default: true (avoid duplicate training samples)</remarks>
    public bool SkipDuplicates { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate file extensions during import.
    /// </summary>
    /// <remarks>Default: true (only import video files)</remarks>
    public bool ValidateFileExtensions { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of training samples to import (0 = no limit).
    /// </summary>
    /// <remarks>Default: 0 (import all samples)</remarks>
    public int MaxSamples { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether to automatically import CSV data on startup.
    /// </summary>
    /// <remarks>Default: false (manual import required)</remarks>
    public bool AutoImportOnStartup { get; set; } = false;

    /// <summary>
    /// Gets or sets the backup path for imported training data.
    /// </summary>
    /// <example>"data/backups/training_data_backup.csv"</example>
    public string BackupPath { get; set; } = "data/backups/training_data_backup.csv";
}