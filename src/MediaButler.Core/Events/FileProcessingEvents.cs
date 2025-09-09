using MediaButler.Core.Enums;

namespace MediaButler.Core.Events;

/// <summary>
/// Domain event triggered when a new file is discovered and added to tracking.
/// </summary>
public record FileDiscoveredEvent : BaseEvent
{
    public required string FileHash { get; init; }
    public required string FileName { get; init; }
    public required string OriginalPath { get; init; }
    public long FileSize { get; init; }
}

/// <summary>
/// Domain event triggered when ML classification completes for a file.
/// </summary>
public record FileClassifiedEvent : BaseEvent
{
    public required string FileHash { get; init; }
    public required string FileName { get; init; }
    public required string SuggestedCategory { get; init; }
    public decimal Confidence { get; init; }
    public DateTime ClassifiedAt { get; init; }
}

/// <summary>
/// Domain event triggered when a file's category is confirmed by the user or system.
/// </summary>
public record FileCategoryConfirmedEvent : BaseEvent
{
    public required string FileHash { get; init; }
    public required string FileName { get; init; }
    public required string ConfirmedCategory { get; init; }
    public required string TargetPath { get; init; }
    public string? PreviousSuggestedCategory { get; init; }
}

/// <summary>
/// Domain event triggered when a file is successfully moved to its final location.
/// </summary>
public record FileMovedEvent : BaseEvent
{
    public required string FileHash { get; init; }
    public required string FileName { get; init; }
    public required string OriginalPath { get; init; }
    public required string FinalPath { get; init; }
    public required string Category { get; init; }
    public DateTime MovedAt { get; init; }
}

/// <summary>
/// Domain event triggered when file processing encounters an error.
/// </summary>
public record FileProcessingErrorEvent : BaseEvent
{
    public required string FileHash { get; init; }
    public required string FileName { get; init; }
    public required string ErrorMessage { get; init; }
    public string? Exception { get; init; }
    public FileStatus PreviousStatus { get; init; }
    public FileStatus NewStatus { get; init; }
    public int RetryCount { get; init; }
}

/// <summary>
/// Domain event triggered when a file is marked for retry processing.
/// </summary>
public record FileRetryScheduledEvent : BaseEvent
{
    public required string FileHash { get; init; }
    public required string FileName { get; init; }
    public required string Reason { get; init; }
    public int RetryAttempt { get; init; }
    public int MaxRetries { get; init; }
}

/// <summary>
/// Domain event triggered when a file's processing status changes.
/// </summary>
public record FileStatusChangedEvent : BaseEvent
{
    public required string FileHash { get; init; }
    public required string FileName { get; init; }
    public FileStatus PreviousStatus { get; init; }
    public FileStatus NewStatus { get; init; }
    public string? Reason { get; init; }
}