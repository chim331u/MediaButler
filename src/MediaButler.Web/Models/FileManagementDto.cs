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
    public bool IsToCategorize { get; set; }
    public bool IsNotToMove { get; set; }
    public bool IsNew { get; set; }
    public string? Hash { get; set; }
    public string? OriginalPath { get; set; }
    public string? TargetPath { get; set; }
    public DateTime? CreatedDate { get; set; }
    public DateTime? ClassifiedAt { get; set; }
    public decimal Confidence { get; set; }
    public string? Status { get; set; }
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