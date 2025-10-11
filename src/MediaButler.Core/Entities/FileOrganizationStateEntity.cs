using MediaButler.Core.Common;
using MediaButler.Core.Models;
using MediaButler.Core.Services;

namespace MediaButler.Core.Entities;

/// <summary>
/// Represents the current state of a file organization operation.
/// Persists organization state in database rather than in-memory static dictionary.
/// </summary>
/// <remarks>
/// Following "Simple Made Easy" principles:
/// - Explicit value storage in database (no hidden static state)
/// - Uses BaseEntity for audit trail and soft delete
/// - Enables querying, reporting, and cleanup of stale states
/// - Facilitates debugging by persisting state history
/// </remarks>
public class FileOrganizationStateEntity : BaseEntity
{
    /// <summary>
    /// Unique identifier for this organization state record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// SHA256 hash of the file being organized.
    /// </summary>
    public string FileHash { get; set; } = string.Empty;

    /// <summary>
    /// Current state of the organization operation.
    /// </summary>
    public OrganizationState State { get; set; }

    /// <summary>
    /// Optional additional context about the current state.
    /// </summary>
    public string? StateContext { get; set; }

    /// <summary>
    /// When the state was last updated.
    /// </summary>
    public DateTime StateUpdatedAt { get; set; }
}
