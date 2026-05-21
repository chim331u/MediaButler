using System.ComponentModel.DataAnnotations;

namespace MediaButler.Core.Models.Requests;

/// <summary>
/// Request model for adding a new tracked file.
/// </summary>
public class AddFileRequest
{
    /// <summary>
    /// Full path to the file to track.
    /// </summary>
    [Required(ErrorMessage = "File path is required")]
    [StringLength(500, ErrorMessage = "File path must not exceed 500 characters")]
    public required string FilePath { get; set; }
}

/// <summary>
/// Request model for confirming file category.
/// </summary>
public class ConfirmCategoryRequest
{
    /// <summary>
    /// Confirmed category for the file.
    /// </summary>
    [Required(ErrorMessage = "Category is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Category must be between 1 and 100 characters")]
    [RegularExpression(@"^[A-Za-z0-9\s\-_\.]+$", ErrorMessage = "Category can only contain letters, numbers, spaces, hyphens, underscores, and dots")]
    public required string Category { get; set; }
}

/// <summary>
/// Request model for marking a file as moved.
/// </summary>
public class MarkMovedRequest
{
    /// <summary>
    /// Target path where the file was moved.
    /// </summary>
    [Required(ErrorMessage = "Target path is required")]
    [StringLength(500, ErrorMessage = "Target path must not exceed 500 characters")]
    public required string TargetPath { get; set; }
}

/// <summary>
/// Request model for deleting a file.
/// </summary>
public class DeleteFileRequest
{
    /// <summary>
    /// Optional reason for deleting the file.
    /// </summary>
    [StringLength(200, ErrorMessage = "Reason must not exceed 200 characters")]
    public string? Reason { get; set; }
}

/// <summary>
/// Request model for triggering folder scan operations.
/// </summary>
public class ScanFoldersRequest
{
    /// <summary>
    /// Optional timeout in seconds for the scan operation (default: 300).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;
}

/// <summary>
/// Request model for scanning a specific folder.
/// </summary>
public class ScanSpecificFolderRequest
{
    /// <summary>
    /// Full path to the folder to scan.
    /// </summary>
    [Required(ErrorMessage = "Folder path is required")]
    [StringLength(500, ErrorMessage = "Folder path must not exceed 500 characters")]
    public required string FolderPath { get; set; }

    /// <summary>
    /// Optional timeout in seconds for the scan operation (default: 300).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;
}
