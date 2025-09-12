using MediaButler.Core.Common;
using MediaButler.Core.Models;

namespace MediaButler.Core.Services;

/// <summary>
/// Service interface for classifying file operation errors and determining recovery strategies.
/// Provides intelligent error analysis following "Simple Made Easy" principles with clear categorization.
/// </summary>
public interface IErrorClassificationService
{
    /// <summary>
    /// Classifies an error and determines the appropriate recovery strategy.
    /// Analyzes exception details, operation context, and system state to provide actionable guidance.
    /// </summary>
    /// <param name="errorContext">Comprehensive context about the failed operation</param>
    /// <returns>Result containing error classification with recovery recommendations</returns>
    Task<Result<ErrorClassificationResult>> ClassifyErrorAsync(ErrorContext errorContext);

    /// <summary>
    /// Determines if an error should be retried automatically based on classification.
    /// Uses classification results and retry history to make intelligent retry decisions.
    /// </summary>
    /// <param name="errorContext">Context of the error being evaluated</param>
    /// <param name="previousClassification">Previous classification result if available</param>
    /// <returns>Result containing retry recommendation with timing and parameters</returns>
    Task<Result<ErrorRecoveryAction>> DetermineRecoveryActionAsync(
        ErrorContext errorContext, 
        ErrorClassificationResult? previousClassification = null);

    /// <summary>
    /// Records an error occurrence for pattern analysis and learning.
    /// Updates error classification accuracy and recovery success rates over time.
    /// </summary>
    /// <param name="errorContext">Context of the error being recorded</param>
    /// <param name="classification">The classification that was applied</param>
    /// <param name="recoveryAction">The recovery action that was taken</param>
    /// <param name="wasSuccessful">Whether the recovery action was successful</param>
    /// <returns>Result indicating success or failure of the recording operation</returns>
    Task<Result> RecordErrorOutcomeAsync(
        ErrorContext errorContext,
        ErrorClassificationResult classification,
        ErrorRecoveryAction recoveryAction,
        bool wasSuccessful);

    /// <summary>
    /// Gets error statistics for monitoring and analysis purposes.
    /// Provides insights into error patterns, classification accuracy, and recovery effectiveness.
    /// </summary>
    /// <param name="timeRange">Time range for statistics analysis</param>
    /// <returns>Result containing comprehensive error statistics</returns>
    Task<Result<ErrorStatistics>> GetErrorStatisticsAsync(TimeSpan timeRange);

    /// <summary>
    /// Validates system state to identify potential error causes.
    /// Proactively checks for common issues like disk space, permissions, and connectivity.
    /// </summary>
    /// <param name="operationContext">Context of the operation to be validated</param>
    /// <returns>Result containing potential issues and preventive recommendations</returns>
    Task<Result<SystemValidationResult>> ValidateSystemStateAsync(ErrorContext operationContext);
}

/// <summary>
/// Statistics about error occurrences and recovery effectiveness.
/// Provides insights for system monitoring and improvement.
/// </summary>
public record ErrorStatistics
{
    /// <summary>
    /// Total number of errors recorded in the time period.
    /// </summary>
    public int TotalErrors { get; init; }

    /// <summary>
    /// Breakdown of errors by classification type.
    /// </summary>
    public Dictionary<string, int> ErrorsByType { get; init; } = new();

    /// <summary>
    /// Success rate of automatic retries by error type.
    /// </summary>
    public Dictionary<string, double> RetrySuccessRates { get; init; } = new();

    /// <summary>
    /// Average time to recovery for each error type.
    /// </summary>
    public Dictionary<string, TimeSpan> AverageRecoveryTimes { get; init; } = new();

    /// <summary>
    /// Most common error patterns and their frequencies.
    /// </summary>
    public IReadOnlyList<ErrorPattern> CommonPatterns { get; init; } = Array.Empty<ErrorPattern>();

    /// <summary>
    /// Classification accuracy metrics.
    /// </summary>
    public double ClassificationAccuracy { get; init; }

    /// <summary>
    /// Trend analysis showing error rate changes over time.
    /// </summary>
    public ErrorTrend Trend { get; init; } = new();
}

/// <summary>
/// Represents a common error pattern identified through analysis.
/// </summary>
public record ErrorPattern
{
    /// <summary>
    /// Description of the error pattern.
    /// </summary>
    public string Pattern { get; init; } = string.Empty;

    /// <summary>
    /// Frequency of this pattern's occurrence.
    /// </summary>
    public int Frequency { get; init; }

    /// <summary>
    /// Success rate of recovery for this pattern.
    /// </summary>
    public double SuccessRate { get; init; }

    /// <summary>
    /// Recommended prevention or mitigation strategies.
    /// </summary>
    public IReadOnlyList<string> RecommendedMitigations { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Trend analysis for error rates over time.
/// </summary>
public record ErrorTrend
{
    /// <summary>
    /// Whether error rates are increasing, decreasing, or stable.
    /// </summary>
    public TrendDirection Direction { get; init; }

    /// <summary>
    /// Percentage change in error rate compared to previous period.
    /// </summary>
    public double PercentageChange { get; init; }

    /// <summary>
    /// Confidence level in the trend analysis.
    /// </summary>
    public double Confidence { get; init; }
}

/// <summary>
/// Direction of error trend analysis.
/// </summary>
public enum TrendDirection
{
    /// <summary>
    /// Error rates are decreasing over time.
    /// </summary>
    Decreasing,

    /// <summary>
    /// Error rates are stable with no significant change.
    /// </summary>
    Stable,

    /// <summary>
    /// Error rates are increasing over time.
    /// </summary>
    Increasing
}

/// <summary>
/// Result of system validation for error prevention.
/// </summary>
public record SystemValidationResult
{
    /// <summary>
    /// Whether the system state is healthy for the operation.
    /// </summary>
    public bool IsHealthy { get; init; }

    /// <summary>
    /// Potential issues identified that could cause errors.
    /// </summary>
    public IReadOnlyList<string> PotentialIssues { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Preventive recommendations to avoid errors.
    /// </summary>
    public IReadOnlyList<string> PreventiveActions { get; init; } = Array.Empty<string>();

    /// <summary>
    /// System resource information relevant to the operation.
    /// </summary>
    public Dictionary<string, object> SystemInfo { get; init; } = new();
}