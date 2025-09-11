using MediaButler.Core.Common;
using MediaButler.Core.Entities;

namespace MediaButler.Services.Interfaces;

/// <summary>
/// Service for generating target file paths using simple template-based rules.
/// Handles path sanitization, conflict resolution, and cross-platform compatibility.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Only handles path generation and validation
/// - No complecting: Separate from file operations and organization logic
/// - Values over state: Pure path generation functions from inputs
/// - Simple templates: String-based templates instead of complex rule engines
/// </remarks>
public interface IPathGenerationService
{
    /// <summary>
    /// Generates the target path for a tracked file based on category and template.
    /// </summary>
    /// <param name="trackedFile">The file to generate a path for</param>
    /// <param name="category">The target category for organization</param>
    /// <param name="template">The path template to use (optional, uses default if null)</param>
    /// <returns>Result containing the generated path or error information</returns>
    Task<Result<string>> GenerateTargetPathAsync(
        TrackedFile trackedFile, 
        string category, 
        string? template = null);

    /// <summary>
    /// Validates a target path for potential issues and conflicts.
    /// </summary>
    /// <param name="targetPath">The path to validate</param>
    /// <param name="originalFilename">The original filename for context</param>
    /// <returns>Result containing validation result or error information</returns>
    Result<PathValidationResult> ValidateTargetPath(string targetPath, string originalFilename);

    /// <summary>
    /// Sanitizes path components for cross-platform safety and character restrictions.
    /// </summary>
    /// <param name="pathComponent">The path component to sanitize (directory or filename)</param>
    /// <returns>Result containing sanitized path component</returns>
    Result<string> SanitizePathComponent(string pathComponent);

    /// <summary>
    /// Resolves path conflicts by generating alternative paths when target already exists.
    /// </summary>
    /// <param name="basePath">The base target path</param>
    /// <param name="filename">The filename to place</param>
    /// <param name="maxAttempts">Maximum number of conflict resolution attempts</param>
    /// <returns>Result containing resolved path that doesn't conflict</returns>
    Task<Result<string>> ResolvePathConflictsAsync(
        string basePath, 
        string filename, 
        int maxAttempts = 10);

    /// <summary>
    /// Gets the default path template for a given category.
    /// </summary>
    /// <param name="category">The category to get template for</param>
    /// <returns>Result containing the default template string</returns>
    Task<Result<string>> GetDefaultTemplateAsync(string category);

    /// <summary>
    /// Previews what the generated path would be without actually creating directories.
    /// </summary>
    /// <param name="trackedFile">The file to preview path for</param>
    /// <param name="category">The target category</param>
    /// <param name="template">The template to use (optional)</param>
    /// <returns>Result containing preview information</returns>
    Task<Result<PathPreviewResult>> PreviewPathGenerationAsync(
        TrackedFile trackedFile, 
        string category, 
        string? template = null);
}

/// <summary>
/// Result of path validation containing potential issues and recommendations.
/// </summary>
public record PathValidationResult
{
    /// <summary>
    /// Whether the path is valid and safe to use.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Whether the target path already exists.
    /// </summary>
    public bool PathExists { get; init; }

    /// <summary>
    /// Whether the target directory is writable.
    /// </summary>
    public bool IsWritable { get; init; }

    /// <summary>
    /// Whether there are any path length issues.
    /// </summary>
    public bool HasLengthIssues { get; init; }

    /// <summary>
    /// Whether there are invalid characters in the path.
    /// </summary>
    public bool HasInvalidCharacters { get; init; }

    /// <summary>
    /// List of validation warnings (non-blocking issues).
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// List of validation errors (blocking issues).
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Recommended actions to resolve issues.
    /// </summary>
    public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Result of path generation preview showing what would happen.
/// </summary>
public record PathPreviewResult
{
    /// <summary>
    /// The generated target path.
    /// </summary>
    public string TargetPath { get; init; } = string.Empty;

    /// <summary>
    /// The sanitized category name used.
    /// </summary>
    public string SanitizedCategory { get; init; } = string.Empty;

    /// <summary>
    /// The sanitized filename used.
    /// </summary>
    public string SanitizedFilename { get; init; } = string.Empty;

    /// <summary>
    /// The template used for generation.
    /// </summary>
    public string TemplateUsed { get; init; } = string.Empty;

    /// <summary>
    /// Whether the target directory would need to be created.
    /// </summary>
    public bool RequiresDirectoryCreation { get; init; }

    /// <summary>
    /// Whether there would be a file conflict.
    /// </summary>
    public bool HasConflict { get; init; }

    /// <summary>
    /// Path validation result for the generated path.
    /// </summary>
    public PathValidationResult ValidationResult { get; init; } = new();

    /// <summary>
    /// Additional context information about the generation process.
    /// </summary>
    public IReadOnlyDictionary<string, object> Context { get; init; } = 
        new Dictionary<string, object>();
}