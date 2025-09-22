using FluentValidation;
using MediaButler.Core.Models.Requests;

namespace MediaButler.API.Validators;

/// <summary>
/// Validator for BatchOrganizeRequest to ensure request data is valid and safe.
/// Implements comprehensive validation rules for batch file operations.
/// </summary>
public class BatchOrganizeRequestValidator : AbstractValidator<BatchOrganizeRequest>
{
    /// <summary>
    /// Initializes a new instance of the BatchOrganizeRequestValidator.
    /// </summary>
    public BatchOrganizeRequestValidator()
    {
        // Validate Files collection
        RuleFor(x => x.Files)
            .NotNull()
            .WithMessage("Files list is required")
            .NotEmpty()
            .WithMessage("At least one file must be specified")
            .Must(files => files.Count <= 1000)
            .WithMessage("Maximum 1000 files per batch operation (ARM32 optimization)")
            .Must(files => files.Count >= 1)
            .WithMessage("Minimum 1 file required for batch operation");

        // Validate each file in the collection
        RuleForEach(x => x.Files)
            .SetValidator(new FileActionDtoValidator());

        // Validate unique file hashes
        RuleFor(x => x.Files)
            .Must(HaveUniqueHashes)
            .WithMessage("Duplicate file hashes are not allowed in a single batch")
            .When(x => x.Files != null && x.Files.Any());

        // Validate batch name if provided
        RuleFor(x => x.BatchName)
            .MaximumLength(200)
            .WithMessage("Batch name must be 200 characters or less")
            .Must(name => string.IsNullOrWhiteSpace(name) || !name.Contains('\0'))
            .WithMessage("Batch name contains invalid characters")
            .When(x => !string.IsNullOrWhiteSpace(x.BatchName));

        // Validate concurrency settings
        RuleFor(x => x.MaxConcurrency)
            .InclusiveBetween(1, 10)
            .WithMessage("Max concurrency must be between 1 and 10 (ARM32 optimization)")
            .When(x => x.MaxConcurrency.HasValue);

        // Validate logical combinations
        RuleFor(x => x)
            .Must(request => !request.DryRun || request.ValidateTargetPaths)
            .WithMessage("Target path validation should be enabled for dry runs")
            .When(x => x.DryRun);

        // Warning for large batches
        RuleFor(x => x.Files)
            .Must(files => files.Count <= 100)
            .WithMessage("Large batch detected (>100 files). Consider splitting for better performance.")
            .WithSeverity(Severity.Warning)
            .When(x => x.Files != null);
    }

    /// <summary>
    /// Validates that all file hashes in the batch are unique.
    /// </summary>
    private static bool HaveUniqueHashes(List<FileActionDto> files)
    {
        if (files == null || files.Count == 0)
            return true;

        var hashes = files.Select(f => f.Hash?.ToUpperInvariant()).Where(h => !string.IsNullOrWhiteSpace(h));
        return hashes.Count() == hashes.Distinct().Count();
    }
}

/// <summary>
/// Validator for individual FileActionDto within a batch request.
/// Ensures each file action has valid and properly formatted data.
/// </summary>
public class FileActionDtoValidator : AbstractValidator<FileActionDto>
{
    /// <summary>
    /// Initializes a new instance of the FileActionDtoValidator.
    /// </summary>
    public FileActionDtoValidator()
    {
        // Validate Hash (SHA256)
        RuleFor(x => x.Hash)
            .NotEmpty()
            .WithMessage("File hash is required")
            .Length(64)
            .WithMessage("File hash must be exactly 64 characters (SHA256)")
            .Matches("^[a-fA-F0-9]{64}$")
            .WithMessage("File hash must contain only hexadecimal characters");

        // Validate ConfirmedCategory
        RuleFor(x => x.ConfirmedCategory)
            .NotEmpty()
            .WithMessage("Confirmed category is required")
            .MaximumLength(100)
            .WithMessage("Category name must be 100 characters or less")
            .Must(BeValidCategoryName)
            .WithMessage("Category name contains invalid characters for file system paths")
            .Must(category => !string.IsNullOrWhiteSpace(category?.Trim()))
            .WithMessage("Category name cannot be empty or whitespace only");

        // Validate CustomTargetPath if provided
        RuleFor(x => x.CustomTargetPath)
            .MaximumLength(500)
            .WithMessage("Custom target path must be 500 characters or less")
            .Must(BeValidPath)
            .WithMessage("Custom target path contains invalid characters")
            .When(x => !string.IsNullOrWhiteSpace(x.CustomTargetPath));

        // Validate Metadata if provided
        RuleFor(x => x.Metadata)
            .Must(metadata => metadata == null || metadata.Count <= 20)
            .WithMessage("Maximum 20 metadata entries allowed per file")
            .When(x => x.Metadata != null);

        // Validate metadata keys and values
        RuleFor(x => x.Metadata)
            .Must(HaveValidMetadataKeys)
            .WithMessage("Metadata keys must be non-empty strings with max 50 characters")
            .Must(HaveValidMetadataValues)
            .WithMessage("Metadata values must be serializable and under 1000 characters when serialized")
            .When(x => x.Metadata != null && x.Metadata.Any());
    }

    /// <summary>
    /// Validates that a category name is suitable for file system operations.
    /// </summary>
    private static bool BeValidCategoryName(string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            return false;

        // Check for invalid file system characters
        var invalidChars = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).ToHashSet();
        return !categoryName.Any(invalidChars.Contains);
    }

    /// <summary>
    /// Validates that a path is properly formatted and doesn't contain invalid characters.
    /// </summary>
    private static bool BeValidPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true; // Null/empty is valid (optional field)

        try
        {
            // Check for invalid path characters
            var invalidChars = Path.GetInvalidPathChars().ToHashSet();
            if (path.Any(invalidChars.Contains))
                return false;

            // Try to create a path to validate format
            Path.GetFullPath(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates that metadata keys are properly formatted.
    /// </summary>
    private static bool HaveValidMetadataKeys(Dictionary<string, object>? metadata)
    {
        if (metadata == null)
            return true;

        return metadata.Keys.All(key =>
            !string.IsNullOrWhiteSpace(key) &&
            key.Length <= 50 &&
            !key.Contains('\0') &&
            !key.StartsWith("__") // Reserved prefix
        );
    }

    /// <summary>
    /// Validates that metadata values are serializable and reasonable in size.
    /// </summary>
    private static bool HaveValidMetadataValues(Dictionary<string, object>? metadata)
    {
        if (metadata == null)
            return true;

        return metadata.Values.All(value =>
        {
            if (value == null)
                return true;

            // Check for basic serializable types
            var type = value.GetType();
            if (type.IsPrimitive || type == typeof(string) || type == typeof(DateTime) ||
                type == typeof(decimal) || type == typeof(Guid))
            {
                // Check string length
                if (value is string str && str.Length > 1000)
                    return false;

                return true;
            }

            // For complex objects, try to serialize and check size
            try
            {
                var serialized = System.Text.Json.JsonSerializer.Serialize(value);
                return serialized.Length <= 1000;
            }
            catch
            {
                return false; // Not serializable
            }
        });
    }
}