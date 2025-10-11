using MediaButler.Core.Common;
using MediaButler.Core.Entities;
using MediaButler.Core.Models;
using MediaButler.Core.Services;
using MediaButler.Services.Interfaces;

namespace MediaButler.Services.Validation;

/// <summary>
/// Orchestrates multiple focused validators to provide comprehensive organization validation.
/// Following "Simple Made Easy" principles - composes simple validators rather than
/// braiding multiple concerns together.
/// </summary>
public class OrganizationValidator : IOrganizationValidator
{
    private readonly IFileValidator[] _validators;

    public OrganizationValidator()
    {
        // Compose focused validators - each with a single responsibility
        _validators = new IFileValidator[]
        {
            new SourceFileAccessValidator(),
            new TargetDirectoryValidator(),
            new DiskSpaceValidator(),
            new PathLengthValidator(),
            new FileConflictValidator()
        };
    }

    public async Task<Result<OrganizationValidationResult>> ValidateAsync(
        TrackedFile file,
        string targetPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var allIssues = new List<string>();
            var allWarnings = new List<string>();
            var allRecommendations = new List<string>();
            var allDetails = new Dictionary<string, object>();

            // Execute all validators
            foreach (var validator in _validators)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var result = await validator.ValidateAsync(file, targetPath);

                // Aggregate results
                allIssues.AddRange(result.Issues);
                allWarnings.AddRange(result.Warnings);
                allRecommendations.AddRange(result.Recommendations);

                // Merge details (prefixed with validator name to avoid conflicts)
                var validatorName = validator.GetType().Name.Replace("Validator", "");
                foreach (var detail in result.Details)
                {
                    allDetails[$"{validatorName}_{detail.Key}"] = detail.Value;
                }
            }

            var isSafe = allIssues.Count == 0;

            var validationResult = new OrganizationValidationResult
            {
                IsSafe = isSafe,
                SafetyIssues = allIssues,
                Warnings = allWarnings,
                ValidationDetails = allDetails,
                RecommendedActions = allRecommendations
            };

            return Result<OrganizationValidationResult>.Success(validationResult);
        }
        catch (Exception ex)
        {
            return Result<OrganizationValidationResult>.Failure(
                $"Validation failed with unexpected error: {ex.Message}");
        }
    }
}
