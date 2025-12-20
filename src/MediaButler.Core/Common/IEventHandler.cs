using MediaButler.Core.Events;

namespace MediaButler.Core.Common;

/// <summary>
/// Defines a handler for a domain event.
/// Replaces MediatR.INotificationHandler to remove external dependency.
/// </summary>
/// <typeparam name="TEvent">The type of event to handle</typeparam>
public interface IEventHandler<in TEvent> where TEvent : IDomainEvent
{
    /// <summary>
    /// Handles the event.
    /// </summary>
    /// <param name="domainEvent">The event to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
