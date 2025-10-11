using MediaButler.Core.Entities;

namespace MediaButler.Services.Validation;

/// <summary>
/// Validates that the source file exists and is accessible.
/// Single responsibility: Source file accessibility check.
/// </summary>
public class SourceFileAccessValidator : IFileValidator
{
    public async Task<FileValidationResult> ValidateAsync(TrackedFile file, string targetPath)
    {
        var issues = new List<string>();
        var details = new Dictionary<string, object>();

        // Check if source file exists
        if (!File.Exists(file.OriginalPath))
        {
            issues.Add($"Source file not found: {file.OriginalPath}");
            details["SourceFileExists"] = false;
            details["SourceFileAccessible"] = false;
        }
        else
        {
            details["SourceFileExists"] = true;

            // Test file access
            try
            {
                await using var stream = File.OpenRead(file.OriginalPath);
                details["SourceFileAccessible"] = true;
            }
            catch (Exception ex)
            {
                issues.Add($"Cannot access source file: {ex.Message}");
                details["SourceFileAccessible"] = false;
            }
        }

        return new FileValidationResult
        {
            IsValid = issues.Count == 0,
            Issues = issues,
            Details = details
        };
    }
}
