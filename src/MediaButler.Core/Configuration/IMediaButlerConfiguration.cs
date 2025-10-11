namespace MediaButler.Core.Configuration;

/// <summary>
/// Centralized configuration interface for MediaButler application settings.
/// Following "Simple Made Easy" principles by providing a single source of truth for configuration values.
/// </summary>
public interface IMediaButlerConfiguration
{
    /// <summary>
    /// Target directory for organized media files.
    /// Default: "/library"
    /// </summary>
    string MediaLibraryPath { get; }

    /// <summary>
    /// Primary directory monitored for new files.
    /// Default: "../../temp/watch"
    /// </summary>
    string WatchFolderPath { get; }

    /// <summary>
    /// Directory for files awaiting user confirmation.
    /// Default: "/tmp/mediabutler/pending"
    /// </summary>
    string PendingReviewPath { get; }

    /// <summary>
    /// Maximum number of retry attempts for file processing operations.
    /// Default: 3
    /// </summary>
    int MaxRetryCount { get; }

    /// <summary>
    /// Confidence threshold for automatic file classification (0.0 - 1.0).
    /// Files with confidence above this threshold are auto-classified.
    /// Default: 0.85
    /// </summary>
    decimal AutoClassifyThreshold { get; }

    /// <summary>
    /// Minimum confidence threshold for classification suggestions (0.0 - 1.0).
    /// Files with confidence above this threshold show suggestions to user.
    /// Default: 0.50
    /// </summary>
    decimal SuggestionThreshold { get; }

    /// <summary>
    /// Maximum allowed processing time for classification in milliseconds.
    /// Default: 500
    /// </summary>
    int MaxClassificationTimeMs { get; }

    /// <summary>
    /// Maximum files per batch operation (ARM32 memory constraint).
    /// Default: 50
    /// </summary>
    int MaxBatchSize { get; }
}
