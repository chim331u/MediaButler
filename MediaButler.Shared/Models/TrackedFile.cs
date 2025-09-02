namespace MediaButler.Shared.Models;

public class TrackedFile
{
    public required string Hash { get; init; }
    public required string FilePath { get; init; }
    public required string OriginalFileName { get; init; }
    public required long FileSizeBytes { get; init; }
    public required DateTime DiscoveredAt { get; init; }
    
    public string? Category { get; init; }
    public double? Confidence { get; init; }
    public FileStatus Status { get; init; }
    public DateTime? ClassifiedAt { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public DateTime? MovedAt { get; init; }
    public string? MovedToPath { get; init; }
    
    public int RetryCount { get; init; }
    public string? LastError { get; init; }
    public DateTime? LastAttemptAt { get; init; }
}