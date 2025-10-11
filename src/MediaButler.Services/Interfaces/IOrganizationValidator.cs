using MediaButler.Core.Common;
using MediaButler.Core.Entities;
using MediaButler.Core.Models;
using MediaButler.Core.Services;

namespace MediaButler.Services.Interfaces;

/// <summary>
/// Validates file organization operations for safety and feasibility.
/// Following "Simple Made Easy" principles by separating validation concerns
/// from orchestration logic.
/// </summary>
public interface IOrganizationValidator
{
    /// <summary>
    /// Performs comprehensive validation of a file organization operation.
    /// </summary>
    /// <param name="file">The tracked file to be organized</param>
    /// <param name="targetPath">The proposed target path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result containing safety status, issues, and recommendations</returns>
    Task<Result<OrganizationValidationResult>> ValidateAsync(
        TrackedFile file,
        string targetPath,
        CancellationToken cancellationToken = default);
}
