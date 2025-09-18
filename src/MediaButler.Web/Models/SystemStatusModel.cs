namespace MediaButler.Web.Models;

/// <summary>
/// Represents comprehensive system status information for dashboard display
/// </summary>
public record SystemStatusModel
{
    // File Statistics
    public int? TotalFiles { get; init; }
    public int? PendingFiles { get; init; }
    public int? ProcessedFiles { get; init; }
    public int? ErrorFiles { get; init; }

    // System Health
    public bool IsApiHealthy { get; init; } = true;
    public int? MemoryUsageMB { get; init; }
    public TimeSpan? UptimeSpan { get; init; }

    // Processing Queue
    public int? QueueSize { get; init; }
    public int? ActiveJobs { get; init; }
    public int? CompletedToday { get; init; }
    public int? AvgProcessingTimeMs { get; init; }

    // Storage Information
    public double? StorageUsedGB { get; init; }
    public double? StorageTotalGB { get; init; }
    public double? FilesTotalSizeGB { get; init; }

    // Timestamps
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    // Computed Properties
    public double StorageUsagePercentage => StorageTotalGB > 0 ? (StorageUsedGB ?? 0) / StorageTotalGB.Value * 100 : 0;
    public double StorageAvailableGB => (StorageTotalGB ?? 0) - (StorageUsedGB ?? 0);
    public int ProcessingSuccessRate => TotalFiles > 0 ? (int)((ProcessedFiles ?? 0) * 100.0 / TotalFiles.Value) : 0;
}