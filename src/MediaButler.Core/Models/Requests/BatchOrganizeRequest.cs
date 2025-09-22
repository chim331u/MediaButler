using System.ComponentModel.DataAnnotations;

namespace MediaButler.Core.Models.Requests;

/// <summary>
/// Request model for batch file organization operations.
/// Supports processing multiple files in a single background job with configurable error handling.
/// </summary>
public class BatchOrganizeRequest
{
    /// <summary>
    /// Gets or sets the list of files to be organized.
    /// Each file includes its hash and confirmed category.
    /// </summary>
    [Required]
    public required List<FileActionDto> Files { get; set; }

    /// <summary>
    /// Gets or sets whether to continue processing remaining files if one fails.
    /// Default is false (abort on first error).
    /// </summary>
    public bool ContinueOnError { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to validate target paths before processing.
    /// Default is true for safety.
    /// </summary>
    public bool ValidateTargetPaths { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to create target directories if they don't exist.
    /// Default is true for convenience.
    /// </summary>
    public bool CreateDirectories { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this is a dry run (validation only, no actual file movement).
    /// Default is false (perform actual operations).
    /// </summary>
    public bool DryRun { get; set; } = false;

    /// <summary>
    /// Gets or sets an optional name for this batch operation.
    /// Used for logging and user identification.
    /// </summary>
    public string? BatchName { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of concurrent file operations.
    /// If null, uses system default. Useful for ARM32 resource management.
    /// </summary>
    public int? MaxConcurrency { get; set; }
}

/// <summary>
/// Represents a single file action within a batch operation.
/// Contains the file reference and target category information.
/// </summary>
public class FileActionDto
{
    /// <summary>
    /// Gets or sets the SHA256 hash of the file to be processed.
    /// This serves as the unique identifier for the file.
    /// </summary>
    [Required]
    [StringLength(64, MinimumLength = 64)]
    public required string Hash { get; set; }

    /// <summary>
    /// Gets or sets the confirmed category for file organization.
    /// This is the final category decision, either from ML or user input.
    /// </summary>
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string ConfirmedCategory { get; set; }

    /// <summary>
    /// Gets or sets a custom target path for this specific file.
    /// If null, the path will be generated based on the confirmed category.
    /// </summary>
    public string? CustomTargetPath { get; set; }

    /// <summary>
    /// Gets or sets additional metadata for this file action.
    /// Can be used for custom processing instructions.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}