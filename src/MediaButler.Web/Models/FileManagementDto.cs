namespace MediaButler.Web.Models;

/// <summary>
/// DTO for file management operations matching the FC_WEB structure
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

    // Helper methods to replace the removed boolean properties
    public bool IsToCategorize => Status == "Classified" || GetStatusCode() == 2;
    public bool IsNotToMove => IsInMoveQueue || Status == "Moved" || Status == "Error" || Status == "Ignored" ||
                               GetStatusCode() == 5 || GetStatusCode() == 6 || GetStatusCode() == 8;
    public bool IsNew => Status == "New" || Status == "Processing" ||
                         GetStatusCode() == 0 || GetStatusCode() == 1;

    private int GetStatusCode()
    {
        // Try to extract status code from status string if it's in format "Status X"
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
/// Enum for move file results used in SignalR notifications
/// </summary>
public enum MoveFilesResults
{
    Started,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Filter parameters for file listing
/// </summary>
public class FileFilterParams
{
    public const int All = 1;
    public const int Categorized = 2;
    public const int ToCategorize = 3;
}