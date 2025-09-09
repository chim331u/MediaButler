using MediatR;

namespace MediaButler.Core.Events;

/// <summary>
/// Marker interface for domain events in the MediaButler system.
/// Domain events represent significant business occurrences that other parts of the system may need to react to.
/// Following "Simple Made Easy" principles, events are immutable value objects that describe what happened.
/// </summary>
/// <remarks>
/// Domain events decouple the core business logic from side effects and cross-cutting concerns.
/// This allows the system to remain simple by avoiding direct dependencies between unrelated components.
/// Events are processed asynchronously via MediatR to maintain responsiveness.
/// </remarks>
public interface IDomainEvent : INotification
{
    /// <summary>
    /// Gets the timestamp when this domain event occurred.
    /// </summary>
    DateTime OccurredAt { get; }

    /// <summary>
    /// Gets the unique identifier for this event instance.
    /// </summary>
    Guid EventId { get; }
}