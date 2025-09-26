using MediaButler.Core.Entities;

namespace MediaButler.Core.Models;

/// <summary>
/// Represents a single file organization operation within a batch.
/// Contains all necessary information to move a file from its current location to the target location.
/// </summary>
public class FileOrganizeOperation
{
    /// <summary>
    /// Gets or sets the tracked file entity from the database.
    /// Contains the complete file metadata and current state.
    /// </summary>
    public required TrackedFile TrackedFile { get; set; }

    /// <summary>
    /// Gets or sets the confirmed category for this file.
    /// This is the final category decision used for organization.
    /// </summary>
    public required string ConfirmedCategory { get; set; }

    /// <summary>
    /// Gets or sets the calculated target path for the file.
    /// This is where the file should be moved to.
    /// </summary>
    public required string TargetPath { get; set; }

    /// <summary>
    /// Gets or sets the current source path of the file.
    /// This is where the file currently exists on the file system.
    /// </summary>
    public required string SourcePath { get; set; }

    /// <summary>
    /// Gets or sets custom metadata for this specific operation.
    /// Can be used for operation-specific configuration or tracking.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets whether this operation has been validated.
    /// Used to track pre-processing validation status.
    /// </summary>
    public bool IsValidated { get; set; }

    /// <summary>
    /// Gets or sets any validation errors found during pre-processing.
    /// If not null, this operation should be skipped or handled specially.
    /// </summary>
    public string? ValidationError { get; set; }

    /// <summary>
    /// Gets or sets the priority of this operation within the batch.
    /// Higher numbers indicate higher priority. Default is 0.
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Gets or sets the estimated size impact of this operation.
    /// Used for progress tracking and resource management.
    /// </summary>
    public long EstimatedSizeBytes => TrackedFile.FileSize;

    /// <summary>
    /// Gets or sets additional files that should be moved with this file.
    /// For example, subtitle files, metadata files, etc.
    /// </summary>
    public List<string> RelatedFiles { get; set; } = new();

    /// <summary>
    /// Gets a descriptive name for this operation for logging and UI purposes.
    /// </summary>
    public string DisplayName => $"{TrackedFile.FileName} → {ConfirmedCategory}";

    /// <summary>
    /// Creates a new FileOrganizeOperation from a TrackedFile and target information.
    /// </summary>
    /// <param name="trackedFile">The file to be organized</param>
    /// <param name="confirmedCategory">The target category</param>
    /// <param name="targetPath">The calculated target path</param>
    /// <returns>A new FileOrganizeOperation instance</returns>
    public static FileOrganizeOperation Create(
        TrackedFile trackedFile,
        string confirmedCategory,
        string targetPath)
    {
        return new FileOrganizeOperation
        {
            TrackedFile = trackedFile,
            ConfirmedCategory = confirmedCategory,
            TargetPath = targetPath,
            SourcePath = trackedFile.OriginalPath
        };
    }

    /// <summary>
    /// Validates this operation and sets validation status.
    /// </summary>
    /// <returns>True if the operation is valid, false otherwise</returns>
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(ConfirmedCategory))
        {
            ValidationError = "Confirmed category is required";
            IsValidated = false;
            return false;
        }

        if (string.IsNullOrWhiteSpace(TargetPath))
        {
            ValidationError = "Target path is required";
            IsValidated = false;
            return false;
        }

        if (string.IsNullOrWhiteSpace(SourcePath))
        {
            ValidationError = "Source path is required";
            IsValidated = false;
            return false;
        }

        if (!File.Exists(SourcePath))
        {
            ValidationError = $"Source file does not exist: {SourcePath}";
            IsValidated = false;
            return false;
        }

        IsValidated = true;
        ValidationError = null;
        return true;
    }
}