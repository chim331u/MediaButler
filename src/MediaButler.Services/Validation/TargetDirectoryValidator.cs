using MediaButler.Core.Entities;

namespace MediaButler.Services.Validation;

/// <summary>
/// Validates that the target directory exists or can be created and is writable.
/// Single responsibility: Target directory validation.
/// </summary>
public class TargetDirectoryValidator : IFileValidator
{
    public async Task<FileValidationResult> ValidateAsync(TrackedFile file, string targetPath)
    {
        var issues = new List<string>();
        var warnings = new List<string>();
        var recommendations = new List<string>();
        var details = new Dictionary<string, object>();

        var targetDirectory = Path.GetDirectoryName(targetPath);

        if (string.IsNullOrEmpty(targetDirectory))
        {
            issues.Add("Invalid target path - no directory specified");
            return new FileValidationResult
            {
                IsValid = false,
                Issues = issues,
                Details = details
            };
        }

        // Check if target directory exists or can be created
        try
        {
            if (!Directory.Exists(targetDirectory))
            {
                warnings.Add($"Target directory will be created: {targetDirectory}");
                details["TargetDirectoryExists"] = false;

                // Create the directory to test write permissions
                Directory.CreateDirectory(targetDirectory);
            }
            else
            {
                details["TargetDirectoryExists"] = true;
            }

            // Test write permissions
            var testFile = Path.Combine(targetDirectory, $"test_{Guid.NewGuid()}.tmp");
            await File.WriteAllTextAsync(testFile, "test");
            File.Delete(testFile);
            details["TargetDirectoryWritable"] = true;
        }
        catch (Exception ex)
        {
            issues.Add($"Cannot write to target directory: {ex.Message}");
            recommendations.Add("Check directory permissions and ensure the path is writable");
            details["TargetDirectoryWritable"] = false;
        }

        return new FileValidationResult
        {
            IsValid = issues.Count == 0,
            Issues = issues,
            Warnings = warnings,
            Recommendations = recommendations,
            Details = details
        };
    }
}
