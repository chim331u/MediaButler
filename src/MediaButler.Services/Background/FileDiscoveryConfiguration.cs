using System.ComponentModel.DataAnnotations;

namespace MediaButler.Services.Background;

/// <summary>
/// Configuration settings for file discovery and monitoring operations.
/// Follows "Simple Made Easy" principles with explicit, validated configuration.
/// </summary>
public class FileDiscoveryConfiguration
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "MediaButler:FileDiscovery";

    /// <summary>
    /// List of folder paths to monitor for new files.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one watch folder must be configured")]
    public List<string> WatchFolders { get; set; } = new();

    /// <summary>
    /// Whether to enable FileSystemWatcher for real-time file detection.
    /// If false, only periodic scanning will be performed.
    /// </summary>
    public bool EnableFileSystemWatcher { get; set; } = true;

    /// <summary>
    /// Interval in minutes for periodic folder scanning.
    /// Used as backup when FileSystemWatcher is disabled or for catching missed events.
    /// </summary>
    [Range(1, 1440, ErrorMessage = "Scan interval must be between 1 and 1440 minutes")]
    public int ScanIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// File extensions to monitor for (case-insensitive).
    /// Only files with these extensions will be processed.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one file extension must be configured")]
    public List<string> FileExtensions { get; set; } = new() { ".mkv", ".mp4", ".avi", ".m4v", ".wmv" };

    /// <summary>
    /// Regex patterns for files/folders to exclude from monitoring.
    /// Useful for excluding temporary, incomplete, or system files.
    /// </summary>
    public List<string> ExcludePatterns { get; set; } = new() { @".*\.tmp$", @".*\.part$", @".*\.incomplete$" };

    /// <summary>
    /// Minimum file size in MB to consider for processing.
    /// Helps filter out small/incomplete files.
    /// </summary>
    [Range(0, 100000, ErrorMessage = "Minimum file size must be between 0 and 100000 MB")]
    public double MinFileSizeMB { get; set; } = 1.0;

    /// <summary>
    /// Delay in seconds before processing a newly detected file.
    /// Helps ensure file writes are complete before processing.
    /// </summary>
    [Range(0, 300, ErrorMessage = "Debounce delay must be between 0 and 300 seconds")]
    public int DebounceDelaySeconds { get; set; } = 3;

    /// <summary>
    /// Maximum number of concurrent folder scan operations.
    /// ARM32 optimization to prevent resource exhaustion.
    /// </summary>
    [Range(1, 10, ErrorMessage = "Max concurrent scans must be between 1 and 10")]
    public int MaxConcurrentScans { get; set; } = 2;

    /// <summary>
    /// Validates the configuration and returns validation results.
    /// </summary>
    /// <returns>Collection of validation errors, if any</returns>
    public IEnumerable<ValidationResult> Validate()
    {
        var context = new ValidationContext(this);
        var results = new List<ValidationResult>();
        
        // Perform built-in validation
        Validator.TryValidateObject(this, context, results, validateAllProperties: true);

        // Custom validation logic
        foreach (var folder in WatchFolders)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                results.Add(new ValidationResult("Watch folder cannot be null or empty", new[] { nameof(WatchFolders) }));
            }
        }

        foreach (var extension in FileExtensions)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                results.Add(new ValidationResult("File extension cannot be null or empty", new[] { nameof(FileExtensions) }));
            }
            else if (!extension.StartsWith('.'))
            {
                results.Add(new ValidationResult($"File extension '{extension}' must start with a dot", new[] { nameof(FileExtensions) }));
            }
        }

        return results;
    }

    /// <summary>
    /// Checks if a file extension is monitored by this configuration.
    /// </summary>
    /// <param name="extension">The file extension to check (with or without leading dot)</param>
    /// <returns>True if the extension should be monitored</returns>
    public bool IsExtensionMonitored(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return false;

        var normalizedExtension = extension.StartsWith('.') ? extension : $".{extension}";
        return FileExtensions.Any(ext => 
            string.Equals(ext, normalizedExtension, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if a file path should be excluded based on exclusion patterns.
    /// </summary>
    /// <param name="filePath">The file path to check</param>
    /// <returns>True if the file should be excluded</returns>
    public bool IsFileExcluded(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return true;

        return ExcludePatterns.Any(pattern => 
        {
            try
            {
                return System.Text.RegularExpressions.Regex.IsMatch(filePath, pattern, 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            catch
            {
                // Invalid regex pattern, treat as literal string match
                return filePath.Contains(pattern, StringComparison.OrdinalIgnoreCase);
            }
        });
    }
}