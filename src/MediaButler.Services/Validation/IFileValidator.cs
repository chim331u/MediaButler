using MediaButler.Core.Entities;

namespace MediaButler.Services.Validation;

/// <summary>
/// Interface for individual file validation rules.
/// Each validator implements a single validation concern following
/// "Simple Made Easy" principles - each validator has one role/task/objective.
/// </summary>
public interface IFileValidator
{
    /// <summary>
    /// Validates a specific aspect of file organization.
    /// </summary>
    /// <param name="file">The tracked file being validated</param>
    /// <param name="targetPath">The proposed target path</param>
    /// <returns>Validation result with specific issues found</returns>
    Task<FileValidationResult> ValidateAsync(TrackedFile file, string targetPath);
}

/// <summary>
/// Result of a single file validation check.
/// </summary>
public class FileValidationResult
{
    public bool IsValid { get; init; }
    public List<string> Issues { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public List<string> Recommendations { get; init; } = new();
    public Dictionary<string, object> Details { get; init; } = new();
}
