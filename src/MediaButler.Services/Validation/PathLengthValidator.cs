using MediaButler.Core.Entities;

namespace MediaButler.Services.Validation;

/// <summary>
/// Validates that the target path length is within system limits.
/// Single responsibility: Path length validation.
/// </summary>
public class PathLengthValidator : IFileValidator
{
    private const int WindowsPathLengthLimit = 260;

    public Task<FileValidationResult> ValidateAsync(TrackedFile file, string targetPath)
    {
        var issues = new List<string>();
        var recommendations = new List<string>();
        var details = new Dictionary<string, object>();

        details["TargetPathLength"] = targetPath.Length;
        details["PathLengthLimit"] = WindowsPathLengthLimit;

        if (targetPath.Length > WindowsPathLengthLimit)
        {
            issues.Add($"Target path too long: {targetPath.Length} characters (limit: {WindowsPathLengthLimit})");
            recommendations.Add("Choose a shorter category name or enable long path support");
        }

        return Task.FromResult(new FileValidationResult
        {
            IsValid = issues.Count == 0,
            Issues = issues,
            Recommendations = recommendations,
            Details = details
        });
    }
}
