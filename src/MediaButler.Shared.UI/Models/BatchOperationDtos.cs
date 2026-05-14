using System;
using System.Collections.Generic;

namespace MediaButler.Shared.UI.Models;

/// <summary>
/// Request DTO for batch file organization.
/// </summary>
public class BatchOrganizeRequestDto
{
    public required List<FileActionDto> Files { get; set; }
    public bool ContinueOnError { get; set; } = false;
    public bool ValidateTargetPaths { get; set; } = true;
    public bool CreateDirectories { get; set; } = true;
    public bool DryRun { get; set; } = false;
    public string? BatchName { get; set; }
    public int? MaxConcurrency { get; set; }
}

/// <summary>
/// Individual file action within a batch operation.
/// </summary>
public class FileActionDto
{
    public required string Hash { get; set; }
    public required string ConfirmedCategory { get; set; }
    public string? CustomTargetPath { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Response DTO for batch job status.
/// </summary>
public class BatchJobResponseDto
{
    public required string JobId { get; set; }
    public required string Status { get; set; } = "Queued";
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int SuccessfulFiles { get; set; }
    public int FailedFiles { get; set; }
    public int ProgressPercentage => TotalFiles > 0 ? (ProcessedFiles * 100) / TotalFiles : 0;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public TimeSpan? AverageProcessingTime { get; set; }
    public List<FileProcessingResultDto>? DetailedResults { get; set; }
}

/// <summary>
/// Result of processing a single file in a batch.
/// </summary>
public class FileProcessingResultDto
{
    public required string FileHash { get; set; }
    public required string FileName { get; set; }
    public required bool Success { get; set; }
    public required string TargetPath { get; set; }
    public string? ActualPath { get; set; }
    public string? Error { get; set; }
    public TimeSpan? ProcessingTime { get; set; }
    public bool IsDryRun { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object>? Metadata { get; set; }
}
