using MediaButler.Core.Common;

namespace MediaButler.Core.Services;

/// <summary>
/// Interface for publishing domain events from entities.
/// Follows "Simple Made Easy" principles with a single responsibility: event publishing.
/// </summary>
public interface IDomainEventPublisher
{
    /// <summary>
    /// Publishes all domain events from the given entity.
    /// </summary>
    /// <param name="entity">The entity containing domain events to publish</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task PublishEventsAsync(BaseEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes all domain events from multiple entities.
    /// </summary>
    /// <param name="entities">The entities containing domain events to publish</param>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task PublishEventsAsync(IEnumerable<BaseEntity> entities, CancellationToken cancellationToken = default);
}