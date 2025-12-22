namespace MediaButler.Mobile.Models;

/// <summary>
/// DTO for file management operations matching the new API structure.
/// Replaces legacy FilesDetailDto with hash-based identification.
/// </summary>
public class FileManagementDto
{
    /// <summary>
    /// Numeric ID (generated from Hash for compatibility).
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// File name with extension.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Assigned category (e.g., "BREAKING BAD").
    /// </summary>
    public string? FileCategory { get; set; }

    /// <summary>
    /// SHA256 hash (primary identifier in new API).
    /// </summary>
    public string? Hash { get; set; }

    /// <summary>
    /// Original file path before processing.
    /// </summary>
    public string? OriginalPath { get; set; }

    /// <summary>
    /// Target path for organized file.
    /// </summary>
    public string? TargetPath { get; set; }

    /// <summary>
    /// When the file was first discovered.
    /// </summary>
    public DateTime? CreatedDate { get; set; }

    /// <summary>
    /// When the file was classified by ML.
    /// </summary>
    public DateTime? ClassifiedAt { get; set; }

    /// <summary>
    /// ML classification confidence (0-100).
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// Current file status (New, Classified, Moved, etc).
    /// </summary>
    public string? Status { get; set; }

    // ====================================
    // Computed properties for legacy compatibility
    // ====================================

    /// <summary>
    /// File is ready to be moved (Classified or ReadyToMove status).
    /// </summary>
    public bool IsToCategorize => Status == "Classified" || Status == "ReadyToMove" || GetStatusCode() == 2 || GetStatusCode() == 3;

    /// <summary>
    /// File should not be moved (already Moved, Error, or Ignored).
    /// </summary>
    public bool IsNotToMove => Status == "Moved" || Status == "Error" || Status == "Ignored" ||
                               GetStatusCode() == 5 || GetStatusCode() == 6 || GetStatusCode() == 8;

    /// <summary>
    /// File is newly discovered (New or Processing status).
    /// </summary>
    public bool IsNew => Status == "New" || Status == "Processing" ||
                         GetStatusCode() == 0 || GetStatusCode() == 1;

    /// <summary>
    /// Extracts numeric status code from status string if formatted as "Status X".
    /// </summary>
    private int GetStatusCode()
    {
        // Try to extract from "Status X" format
        if (Status?.StartsWith("Status ") == true && int.TryParse(Status.Substring(7), out int code))
            return code;

        // Map string status to code
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
/// Paginated response for file listing with server-side pagination.
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
