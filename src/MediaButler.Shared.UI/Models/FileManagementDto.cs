using System;
using System.Collections.Generic;

namespace MediaButler.Shared.UI.Models;

/// <summary>
/// DTO for file management operations.
/// Consolidates structures from Web and Mobile for multi-platform use.
/// </summary>
public class FileManagementDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public long FileSize { get; set; }
    public string? FileCategory { get; set; }
    public string? Hash { get; set; }
    public string? OriginalPath { get; set; }
    public string? TargetPath { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? ClassifiedAt { get; set; }
    public decimal Confidence { get; set; }
    public string? Status { get; set; }

    // Move queue management
    public bool IsInMoveQueue { get; set; } = false;

    // Computed properties for logic consistency across platforms
    public bool IsToCategorize => Status == "Classified" || Status == "ReadyToMove" || GetStatusCode() == 2 || GetStatusCode() == 3;
    public bool IsNotToMove => IsInMoveQueue || Status == "Moved" || Status == "Error" || Status == "Ignored" ||
                               GetStatusCode() == 5 || GetStatusCode() == 6 || GetStatusCode() == 8;
    public bool IsNew => Status == "New" || Status == "Processing" ||
                         GetStatusCode() == 0 || GetStatusCode() == 1;

    private int GetStatusCode()
    {
        if (Status?.StartsWith("Status ") == true && int.TryParse(Status.Substring(7), out int code))
            return code;

        return Status switch
        {
            "New" => 0,
            "Processing" => 1,
            "Classified" => 2,
            "ReadyToMove" => 3,
            "Moving" => 4,
            "Moved" => 5,
            "Error" => 6,
            "Retry" => 7,
            "Ignored" => 8,
            _ => -1
        };
    }
}

/// <summary>
/// Paginated response for file listing.
/// </summary>
public class PaginatedFilesDto
{
    public List<FileManagementDto> Items { get; set; } = new();
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
}

/// <summary>
/// Scan result DTO for folder scanning operations.
/// </summary>
public class ScanResultDto
{
    public int FilesDiscovered { get; set; }
    public DateTime ScanStartedAt { get; set; }
    public DateTime ScanCompletedAt { get; set; }
    public bool MonitoringEnabled { get; set; }
    public List<string> MonitoredPaths { get; set; } = new();
    public string? ScannedPath { get; set; }
    public double ScanDurationMs { get; set; }
}

/// <summary>
/// Enum for move file results.
/// </summary>
public enum MoveFilesResults
{
    Moved = 0,
    Failed = 1,
    IdNotPresent = 2,
    Completed = 3
}
