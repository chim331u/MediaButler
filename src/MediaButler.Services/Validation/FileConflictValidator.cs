using MediaButler.Core.Entities;

namespace MediaButler.Services.Validation;

/// <summary>
/// Validates potential file conflicts at the target path.
/// Single responsibility: File conflict detection.
/// </summary>
public class FileConflictValidator : IFileValidator
{
    public Task<FileValidationResult> ValidateAsync(TrackedFile file, string targetPath)
    {
        var warnings = new List<string>();
        var recommendations = new List<string>();
        var details = new Dictionary<string, object>();

        if (File.Exists(targetPath))
        {
            warnings.Add($"Target file already exists: {targetPath}");
            recommendations.Add("File will be renamed automatically to avoid conflicts");
            details["ConflictExists"] = true;
        }
        else
        {
            details["ConflictExists"] = false;
        }

        return Task.FromResult(new FileValidationResult
        {
            IsValid = true, // Conflicts are warnings, not failures
            Warnings = warnings,
            Recommendations = recommendations,
            Details = details
        });
    }
}
