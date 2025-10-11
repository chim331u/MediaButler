using MediaButler.Core.Entities;

namespace MediaButler.Services.Validation;

/// <summary>
/// Validates that sufficient disk space is available for the file operation.
/// Single responsibility: Disk space validation.
/// </summary>
public class DiskSpaceValidator : IFileValidator
{
    private const double SpaceBufferMultiplier = 1.1; // 10% buffer

    public Task<FileValidationResult> ValidateAsync(TrackedFile file, string targetPath)
    {
        var issues = new List<string>();
        var recommendations = new List<string>();
        var details = new Dictionary<string, object>();

        var targetDirectory = Path.GetDirectoryName(targetPath) ?? string.Empty;
        var availableSpace = GetAvailableSpace(targetDirectory);
        var requiredSpace = file.FileSize;

        details["AvailableSpaceBytes"] = availableSpace;
        details["RequiredSpaceBytes"] = requiredSpace;
        details["RequiredSpaceWithBuffer"] = (long)(requiredSpace * SpaceBufferMultiplier);

        if (availableSpace < requiredSpace * SpaceBufferMultiplier)
        {
            issues.Add($"Insufficient disk space: {requiredSpace:N0} bytes required, {availableSpace:N0} bytes available");
            recommendations.Add("Free up disk space before attempting organization");
        }

        return Task.FromResult(new FileValidationResult
        {
            IsValid = issues.Count == 0,
            Issues = issues,
            Recommendations = recommendations,
            Details = details
        });
    }

    private static long GetAvailableSpace(string directory)
    {
        try
        {
            if (string.IsNullOrEmpty(directory))
                return 0;

            var drive = new DriveInfo(Path.GetPathRoot(directory) ?? directory);
            return drive.AvailableFreeSpace;
        }
        catch
        {
            return 0; // Default to 0 if we can't determine space
        }
    }
}
