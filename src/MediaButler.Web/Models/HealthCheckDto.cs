namespace MediaButler.Web.Models;

/// <summary>
/// Health check response from MediaButler.API /api/health/detailed endpoint.
/// Matches the actual API response structure.
/// </summary>
public record HealthCheckResponse(
    string Status,
    DateTime Timestamp,
    string Version,
    string Service,
    DatabaseHealth Database,
    MemoryHealth Memory,
    ProcessingHealth Processing,
    object FileOperations,
    object MachineLearning
);

/// <summary>
/// Database health information.
/// </summary>
public record DatabaseHealth(
    string Status,
    int TotalFiles,
    int ProcessedToday
);

/// <summary>
/// Memory usage information.
/// </summary>
public record MemoryHealth(
    double ManagedMemoryMB,
    double WorkingSetMB,
    int TargetLimitMB
);

/// <summary>
/// Processing statistics.
/// </summary>
public record ProcessingHealth(
    Dictionary<string, int> StatusCounts,
    double AverageProcessingTimeMinutes
);

/// <summary>
/// Basic health response from MediaButler.API /api/health endpoint.
/// Used as fallback when detailed endpoint is unavailable.
/// </summary>
public record BasicHealthResponse(
    string Status,
    DateTime Timestamp,
    string Version,
    string Service
);

/// <summary>
/// Simplified health status for UI display.
/// Clear, declarative representation without complexity.
/// </summary>
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// View model for health check display.
/// Separates API concerns from UI concerns.
/// </summary>
public record HealthCheckViewModel(
    HealthStatus OverallStatus,
    string OverallStatusText,
    TimeSpan TotalDuration,
    DateTime LastChecked,
    List<ComponentHealth> Components
);

/// <summary>
/// Individual component health for UI display.
/// Simple, focused data structure.
/// </summary>
public record ComponentHealth(
    string Name,
    HealthStatus Status,
    string StatusText,
    string? Description,
    TimeSpan Duration,
    Dictionary<string, string>? Details = null
);