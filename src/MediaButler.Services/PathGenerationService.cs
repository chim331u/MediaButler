using MediaButler.Core.Common;
using MediaButler.Core.Entities;
using MediaButler.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.RegularExpressions;

namespace MediaButler.Services;

/// <summary>
/// Implementation of path generation service using simple template-based rules.
/// Provides cross-platform path generation with conflict resolution.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Single responsibility: Path generation and validation only
/// - No complecting: Independent from file operations and organization
/// - Values over state: Pure functions for path generation
/// - Simple templates: String-based templates with variable substitution
/// </remarks>
public class PathGenerationService : IPathGenerationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PathGenerationService> _logger;

    // Cross-platform invalid characters for filenames and directories
    private static readonly char[] InvalidPathChars = 
        Path.GetInvalidPathChars().Concat(Path.GetInvalidFileNameChars()).Distinct().ToArray();

    // Additional characters to sanitize for better compatibility
    private static readonly char[] AdditionalSanitizeChars = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };

    // Default path template
    private const string DefaultTemplate = "{MediaLibraryPath}/{Category}/{Filename}";

    // Regex for template variable matching
    private static readonly Regex TemplateVariableRegex = new(@"\{(\w+)\}", RegexOptions.Compiled);

    public PathGenerationService(
        IConfiguration configuration,
        ILogger<PathGenerationService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates target path using template-based substitution.
    /// </summary>
    public async Task<Result<string>> GenerateTargetPathAsync(
        TrackedFile trackedFile, 
        string category, 
        string? template = null)
    {
        if (trackedFile == null)
        {
            return Result<string>.Failure("TrackedFile cannot be null");
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            return Result<string>.Failure("Category cannot be null or empty");
        }

        if (string.IsNullOrWhiteSpace(trackedFile.FileName))
        {
            return Result<string>.Failure("TrackedFile must have a valid FileName");
        }

        try
        {
            _logger.LogDebug("Generating target path for file {Filename} in category {Category}", 
                trackedFile.FileName, category);

            // Get template to use
            var templateResult = await GetTemplateToUseAsync(template, category);
            if (!templateResult.IsSuccess)
            {
                return Result<string>.Failure($"Failed to get template: {templateResult.Error}");
            }

            // Sanitize inputs
            var sanitizedCategoryResult = SanitizePathComponent(category);
            if (!sanitizedCategoryResult.IsSuccess)
            {
                return Result<string>.Failure($"Failed to sanitize category: {sanitizedCategoryResult.Error}");
            }

            var sanitizedFilenameResult = SanitizePathComponent(trackedFile.FileName);
            if (!sanitizedFilenameResult.IsSuccess)
            {
                return Result<string>.Failure($"Failed to sanitize filename: {sanitizedFilenameResult.Error}");
            }

            // Get media library path from static configuration
            var mediaLibraryPath = _configuration["MediaButler:Paths:MediaLibrary"] ?? "/tmp/mediabutler/library";

            // Create template variables
            var variables = new Dictionary<string, string>
            {
                ["MediaLibraryPath"] = mediaLibraryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                ["Category"] = sanitizedCategoryResult.Value,
                ["Filename"] = sanitizedFilenameResult.Value,
                ["OriginalCategory"] = category,
                ["OriginalFilename"] = trackedFile.FileName,
                ["FileHash"] = trackedFile.Hash ?? "unknown",
                ["Extension"] = Path.GetExtension(trackedFile.FileName),
                ["FilenameWithoutExtension"] = Path.GetFileNameWithoutExtension(trackedFile.FileName)
            };

            // Substitute template variables
            var generatedPath = SubstituteTemplateVariables(templateResult.Value, variables);
            
            // Normalize path separators for current platform
            generatedPath = Path.GetFullPath(generatedPath);

            _logger.LogDebug("Generated target path: {TargetPath}", generatedPath);
            
            return Result<string>.Success(generatedPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating target path for file {Filename}", trackedFile.FileName);
            return Result<string>.Failure($"Path generation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates target path for safety and accessibility.
    /// </summary>
    public Result<PathValidationResult> ValidateTargetPath(string targetPath, string originalFilename)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return Result<PathValidationResult>.Failure("Target path cannot be null or empty");
        }

        try
        {
            var warnings = new List<string>();
            var errors = new List<string>();
            var recommendations = new List<string>();

            // Check path length (Windows has 260 char limit, but we'll be more conservative)
            var hasLengthIssues = targetPath.Length > 240;
            if (hasLengthIssues)
            {
                errors.Add($"Path length ({targetPath.Length}) exceeds recommended maximum (240)");
                recommendations.Add("Consider shortening category names or using abbreviations");
            }

            // Check for invalid characters
            var invalidChars = targetPath.Where(c => InvalidPathChars.Contains(c) || AdditionalSanitizeChars.Contains(c)).ToList();
            var hasInvalidCharacters = invalidChars.Count > 0;
            if (hasInvalidCharacters)
            {
                errors.Add($"Path contains invalid characters: {string.Join(", ", invalidChars.Distinct())}");
                recommendations.Add("Use SanitizePathComponent to clean the path");
            }

            // Check if path exists
            var pathExists = File.Exists(targetPath);
            if (pathExists)
            {
                warnings.Add("Target file already exists - may require conflict resolution");
                recommendations.Add("Use ResolvePathConflictsAsync to handle duplicates");
            }

            // Check directory writability
            var directory = Path.GetDirectoryName(targetPath);
            var isWritable = true;
            try
            {
                if (!string.IsNullOrEmpty(directory))
                {
                    // Try to create directory if it doesn't exist (for testing)
                    if (!Directory.Exists(directory))
                    {
                        warnings.Add("Target directory does not exist - will need to be created");
                    }
                    else
                    {
                        // Test writability by attempting to get directory info
                        var dirInfo = new DirectoryInfo(directory);
                        isWritable = !dirInfo.Attributes.HasFlag(FileAttributes.ReadOnly);
                    }
                }
            }
            catch (Exception ex)
            {
                isWritable = false;
                errors.Add($"Cannot access target directory: {ex.Message}");
                recommendations.Add("Check directory permissions and path validity");
            }

            var result = new PathValidationResult
            {
                IsValid = errors.Count == 0,
                PathExists = pathExists,
                IsWritable = isWritable,
                HasLengthIssues = hasLengthIssues,
                HasInvalidCharacters = hasInvalidCharacters,
                Warnings = warnings,
                Errors = errors,
                Recommendations = recommendations
            };

            return Result<PathValidationResult>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating target path {TargetPath}", targetPath);
            return Result<PathValidationResult>.Failure($"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Sanitizes path components by removing/replacing invalid characters.
    /// </summary>
    public Result<string> SanitizePathComponent(string pathComponent)
    {
        if (string.IsNullOrWhiteSpace(pathComponent))
        {
            return Result<string>.Failure("Path component cannot be null or empty");
        }

        try
        {
            var sanitized = pathComponent;

            // Replace invalid characters with safe alternatives
            foreach (var invalidChar in InvalidPathChars.Concat(AdditionalSanitizeChars))
            {
                sanitized = sanitized.Replace(invalidChar, '_');
            }

            // Remove multiple consecutive underscores
            sanitized = Regex.Replace(sanitized, "_+", "_");

            // Remove leading/trailing underscores and whitespace
            sanitized = sanitized.Trim('_', ' ', '.');

            // Ensure we don't create empty strings
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                sanitized = "unknown";
            }

            // Handle reserved Windows names (CON, PRN, AUX, etc.)
            var reservedNames = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(sanitized);
            if (reservedNames.Contains(nameWithoutExtension.ToUpperInvariant()))
            {
                sanitized = $"_{sanitized}";
            }

            return Result<string>.Success(sanitized);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sanitizing path component {Component}", pathComponent);
            return Result<string>.Failure($"Sanitization error: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves path conflicts by appending numbers to create unique paths.
    /// </summary>
    public async Task<Result<string>> ResolvePathConflictsAsync(
        string basePath, 
        string filename, 
        int maxAttempts = 10)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return Result<string>.Failure("Base path cannot be null or empty");
        }

        if (string.IsNullOrWhiteSpace(filename))
        {
            return Result<string>.Failure("Filename cannot be null or empty");
        }

        if (maxAttempts <= 0)
        {
            return Result<string>.Failure("Max attempts must be greater than 0");
        }

        try
        {
            var directory = Path.GetDirectoryName(basePath) ?? "";
            var originalPath = Path.Combine(directory, filename);

            // If no conflict, return original path
            if (!File.Exists(originalPath))
            {
                return Result<string>.Success(originalPath);
            }

            var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
            var extension = Path.GetExtension(filename);

            // Try numbered variations
            for (int i = 1; i <= maxAttempts; i++)
            {
                var numberedFilename = $"{nameWithoutExtension} ({i}){extension}";
                var numberedPath = Path.Combine(directory, numberedFilename);

                if (!File.Exists(numberedPath))
                {
                    _logger.LogDebug("Resolved path conflict for {OriginalPath} -> {ResolvedPath}", 
                        originalPath, numberedPath);
                    return Result<string>.Success(numberedPath);
                }
            }

            // If we exhaust attempts, use timestamp
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var timestampFilename = $"{nameWithoutExtension}_{timestamp}{extension}";
            var timestampPath = Path.Combine(directory, timestampFilename);

            _logger.LogWarning("Exhausted numbered attempts for {OriginalPath}, using timestamp: {TimestampPath}", 
                originalPath, timestampPath);

            return Result<string>.Success(timestampPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving path conflicts for {BasePath}/{Filename}", basePath, filename);
            return Result<string>.Failure($"Conflict resolution error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets default template for category from configuration.
    /// </summary>
    public async Task<Result<string>> GetDefaultTemplateAsync(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return Result<string>.Failure("Category cannot be null or empty");
        }

        try
        {
            // For now, return the default template
            // In the future, this could be enhanced to support per-category templates from configuration
            await Task.CompletedTask; // Simulate async work
            
            return Result<string>.Success(DefaultTemplate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting default template for category {Category}", category);
            return Result<string>.Failure($"Template retrieval error: {ex.Message}");
        }
    }

    /// <summary>
    /// Previews path generation without creating any files or directories.
    /// </summary>
    public async Task<Result<PathPreviewResult>> PreviewPathGenerationAsync(
        TrackedFile trackedFile, 
        string category, 
        string? template = null)
    {
        try
        {
            // Generate the target path
            var pathResult = await GenerateTargetPathAsync(trackedFile, category, template);
            if (!pathResult.IsSuccess)
            {
                return Result<PathPreviewResult>.Failure($"Failed to generate path: {pathResult.Error}");
            }

            var targetPath = pathResult.Value;
            var directory = Path.GetDirectoryName(targetPath) ?? "";

            // Get template used
            var templateResult = await GetTemplateToUseAsync(template, category);
            var templateUsed = templateResult.IsSuccess ? templateResult.Value : DefaultTemplate;

            // Sanitize components for display
            var sanitizedCategory = SanitizePathComponent(category);
            var sanitizedFilename = SanitizePathComponent(trackedFile.FileName ?? "");

            // Check requirements and conflicts
            var requiresDirectoryCreation = !Directory.Exists(directory);
            var hasConflict = File.Exists(targetPath);

            // Validate the path
            var validationResult = ValidateTargetPath(targetPath, trackedFile.FileName ?? "");
            if (!validationResult.IsSuccess)
            {
                return Result<PathPreviewResult>.Failure($"Path validation failed: {validationResult.Error}");
            }

            // Create context information
            var context = new Dictionary<string, object>
            {
                ["DirectoryPath"] = directory,
                ["RequiredDirectoryCreation"] = requiresDirectoryCreation,
                ["ConflictDetected"] = hasConflict,
                ["PathLength"] = targetPath.Length,
                ["IsAbsolutePath"] = Path.IsPathRooted(targetPath)
            };

            var preview = new PathPreviewResult
            {
                TargetPath = targetPath,
                SanitizedCategory = sanitizedCategory.IsSuccess ? sanitizedCategory.Value : category,
                SanitizedFilename = sanitizedFilename.IsSuccess ? sanitizedFilename.Value : trackedFile.FileName ?? "",
                TemplateUsed = templateUsed,
                RequiresDirectoryCreation = requiresDirectoryCreation,
                HasConflict = hasConflict,
                ValidationResult = validationResult.Value,
                Context = context
            };

            return Result<PathPreviewResult>.Success(preview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing path generation for file {Filename}", trackedFile.FileName);
            return Result<PathPreviewResult>.Failure($"Preview error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the template to use, falling back to default if needed.
    /// </summary>
    private async Task<Result<string>> GetTemplateToUseAsync(string? template, string category)
    {
        if (!string.IsNullOrWhiteSpace(template))
        {
            return Result<string>.Success(template);
        }

        // Try to get default template for category
        var defaultTemplateResult = await GetDefaultTemplateAsync(category);
        return defaultTemplateResult.IsSuccess 
            ? defaultTemplateResult 
            : Result<string>.Success(DefaultTemplate);
    }

    /// <summary>
    /// Substitutes template variables with actual values.
    /// </summary>
    private static string SubstituteTemplateVariables(string template, Dictionary<string, string> variables)
    {
        return TemplateVariableRegex.Replace(template, match =>
        {
            var variableName = match.Groups[1].Value;
            return variables.TryGetValue(variableName, out var value) ? value : match.Value;
        });
    }
}