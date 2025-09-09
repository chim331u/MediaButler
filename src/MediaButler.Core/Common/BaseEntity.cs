using System;
using MediaButler.Core.Events;

namespace MediaButler.Core.Common;

/// <summary>
/// Abstract base class for all domain entities providing audit trail functionality and soft delete capability.
/// This class follows "Simple Made Easy" principles by providing a single, focused responsibility:
/// tracking entity lifecycle and audit information without complecting business concerns.
/// </summary>
/// <remarks>
/// All entities inheriting from BaseEntity will automatically have:
/// - Audit trail capabilities (CreatedDate, LastUpdateDate)
/// - Soft delete functionality (IsActive flag)
/// - Optional contextual notes
/// - Helper methods for common state transitions
/// </remarks>
public abstract class BaseEntity
{
    /// <summary>
    /// Gets or sets the date and time when this entity was created.
    /// This value is set once during entity creation and should not be modified afterwards.
    /// </summary>
    /// <value>The UTC date and time of entity creation.</value>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the date and time when this entity was last modified.
    /// This value is automatically updated whenever the entity undergoes changes.
    /// </summary>
    /// <value>The UTC date and time of the last modification.</value>
    public DateTime LastUpdateDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets an optional note providing additional context about this entity.
    /// This field can be used to store user comments, processing notes, or other contextual information.
    /// </summary>
    /// <value>An optional string containing contextual notes, or null if no notes are present.</value>
    public string? Note { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this entity is active (not soft-deleted).
    /// When false, this entity is considered logically deleted but remains in the database for audit purposes.
    /// </summary>
    /// <value>true if the entity is active; false if it has been soft-deleted.</value>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Collection of domain events raised by this entity.
    /// Events are processed asynchronously to maintain loose coupling.
    /// </summary>
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>
    /// Gets the collection of domain events raised by this entity.
    /// Events should be processed and cleared after handling.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Marks this entity as modified by updating the LastUpdateDate to the current UTC time.
    /// Call this method whenever making changes to the entity to maintain accurate audit trail.
    /// </summary>
    /// <example>
    /// <code>
    /// var file = new TrackedFile();
    /// file.Category = "THE OFFICE";
    /// file.MarkAsModified(); // Updates LastUpdateDate
    /// </code>
    /// </example>
    public virtual void MarkAsModified()
    {
        LastUpdateDate = DateTime.UtcNow;
    }

    /// <summary>
    /// Performs a soft delete on this entity by setting IsActive to false and updating the audit trail.
    /// The entity remains in the database but is marked as logically deleted.
    /// </summary>
    /// <param name="reason">Optional reason for the soft deletion to be stored in the Note field.</param>
    /// <example>
    /// <code>
    /// file.SoftDelete("File processing failed after 3 retries");
    /// </code>
    /// </example>
    public virtual void SoftDelete(string? reason = null)
    {
        IsActive = false;
        MarkAsModified();
        
        if (!string.IsNullOrWhiteSpace(reason))
        {
            Note = string.IsNullOrWhiteSpace(Note) 
                ? $"Deleted: {reason}" 
                : $"{Note}\nDeleted: {reason}";
        }
    }

    /// <summary>
    /// Restores a soft-deleted entity by setting IsActive to true and updating the audit trail.
    /// </summary>
    /// <param name="reason">Optional reason for the restoration to be stored in the Note field.</param>
    /// <example>
    /// <code>
    /// file.Restore("User requested restoration");
    /// </code>
    /// </example>
    public virtual void Restore(string? reason = null)
    {
        IsActive = true;
        MarkAsModified();
        
        if (!string.IsNullOrWhiteSpace(reason))
        {
            Note = string.IsNullOrWhiteSpace(Note) 
                ? $"Restored: {reason}" 
                : $"{Note}\nRestored: {reason}";
        }
    }

    /// <summary>
    /// Adds a domain event to be published when this entity is saved.
    /// Events represent significant business occurrences that other parts of the system may react to.
    /// </summary>
    /// <param name="domainEvent">The domain event to add.</param>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent ?? throw new ArgumentNullException(nameof(domainEvent)));
    }

    /// <summary>
    /// Removes a specific domain event from the collection.
    /// </summary>
    /// <param name="domainEvent">The domain event to remove.</param>
    protected void RemoveDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Remove(domainEvent);
    }

    /// <summary>
    /// Clears all domain events from the collection.
    /// This is typically called after events have been processed and published.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}