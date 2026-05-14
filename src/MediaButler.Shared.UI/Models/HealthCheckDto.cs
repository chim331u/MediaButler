using System;
using System.Collections.Generic;

namespace MediaButler.Shared.UI.Models;

/// <summary>
/// Health check response from MediaButler.API.
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

public record DatabaseHealth(
    string Status,
    int TotalFiles,
    int ProcessedToday
);

public record MemoryHealth(
    double ManagedMemoryMB,
    double WorkingSetMB,
    int TargetLimitMB
);

public record ProcessingHealth(
    Dictionary<string, int> StatusCounts,
    double AverageProcessingTimeMinutes
);

public record BasicHealthResponse(
    string Status,
    DateTime Timestamp,
    string Version,
    string Service
);

public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

public record HealthCheckViewModel(
    HealthStatus OverallStatus,
    string OverallStatusText,
    TimeSpan TotalDuration,
    DateTime LastChecked,
    List<ComponentHealth> Components
);

public record ComponentHealth(
    string Name,
    HealthStatus Status,
    string StatusText,
    string? Description,
    TimeSpan Duration,
    Dictionary<string, string>? Details = null
);
