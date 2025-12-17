namespace MediaButler.Web.Models;

/// <summary>
/// Server-Sent Events (SSE) event models matching the Go API SSE events
/// </summary>

// Scan events
public record ScanStartedEvent(string ScanId, string Path, DateTime StartTime);
public record ScanFoundEvent(string ScanId, int FileCount);
public record ScanCompletedEvent(string ScanId, int TotalFiles, int NewFiles, DateTime CompletedTime);

// Move events
public record MoveStartedEvent(int FileId, string FileName, string FromPath, string ToPath);
public record MoveProgressEvent(string BatchId, int Completed, int Total, double PercentComplete);
public record MoveCompletedEvent(int FileId, string FileName, bool Success);

// Training events
public record TrainingStartedEvent(string TrainingId, DateTime StartTime);
public record TrainingCompletedEvent(string TrainingId, bool Success, double Accuracy, DateTime CompletedTime);

// Batch events
public record BatchStartedEvent(string BatchId, int TotalFiles, DateTime StartTime);
public record BatchProgressEvent(string BatchId, int Processed, int Total, int Succeeded, int Failed);
public record BatchCompletedEvent(string BatchId, int Total, int Succeeded, int Failed, DateTime CompletedTime);

// Error events
public record ErrorEvent(string EventType, string Message, string? FileId, DateTime Timestamp);

// Connection events
public record ConnectedEvent(string ClientId, DateTime Time);
