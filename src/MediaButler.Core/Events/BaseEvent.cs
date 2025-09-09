namespace MediaButler.Core.Events;

/// <summary>
/// Base implementation for domain events providing common event properties.
/// Follows "Simple Made Easy" principles with immutable, value-based event structure.
/// </summary>
public abstract record BaseEvent : IDomainEvent
{
    /// <summary>
    /// Gets the timestamp when this domain event occurred.
    /// </summary>
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the unique identifier for this event instance.
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();
}