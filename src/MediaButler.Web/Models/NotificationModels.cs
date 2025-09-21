namespace MediaButler.Web.Models;

/// <summary>
/// File discovery notification data
/// </summary>
public class FileDiscoveryNotification
{
    public string FileName { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public DateTime DiscoveredAt { get; init; }
    public long FileSize { get; init; }
    public string FileHash { get; init; } = string.Empty;
}

/// <summary>
/// File processing status notification data
/// </summary>
public class FileProcessingNotification
{
    public string FileHash { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? Category { get; init; }
    public double? Confidence { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime ProcessedAt { get; init; }
}

/// <summary>
/// System status notification data
/// </summary>
public class SystemStatusNotification
{
    public string Component { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Error notification data
/// </summary>
public class ErrorNotification
{
    public string Source { get; init; } = string.Empty;
    public string ErrorType { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? StackTrace { get; init; }
    public DateTime OccurredAt { get; init; }
    public string? FileHash { get; init; }
    public Dictionary<string, object>? Context { get; init; }
}

/// <summary>
/// Progress notification for long-running operations
/// </summary>
public class ProgressNotification
{
    public string OperationId { get; init; } = string.Empty;
    public string OperationType { get; init; } = string.Empty;
    public int CurrentProgress { get; init; }
    public int TotalItems { get; init; }
    public string? CurrentItem { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public TimeSpan? EstimatedTimeRemaining { get; init; }
}

/// <summary>
/// Batch operation notification
/// </summary>
public class BatchOperationNotification
{
    public string BatchId { get; init; } = string.Empty;
    public string OperationType { get; init; } = string.Empty;
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public int TotalCount { get; init; }
    public List<string> FailedItems { get; init; } = new();
    public DateTime CompletedAt { get; init; }
    public TimeSpan Duration { get; init; }
}