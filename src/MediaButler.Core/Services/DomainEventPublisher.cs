using MediaButler.Core.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MediaButler.Core.Services;

/// <summary>
/// Service responsible for publishing domain events from entities.
/// Uses MediatR to decouple event publishers from handlers following "Simple Made Easy" principles.
/// </summary>
public class DomainEventPublisher : IDomainEventPublisher
{
    private readonly IMediator _mediator;
    private readonly ILogger<DomainEventPublisher> _logger;

    public DomainEventPublisher(IMediator mediator, ILogger<DomainEventPublisher> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Publishes all domain events from the given entity.
    /// </summary>
    public async Task PublishEventsAsync(BaseEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity == null)
            return;

        var domainEvents = entity.DomainEvents.ToList();
        
        if (!domainEvents.Any())
            return;

        _logger.LogDebug("Publishing {EventCount} domain events for entity", domainEvents.Count);

        foreach (var domainEvent in domainEvents)
        {
            try
            {
                await _mediator.Publish(domainEvent, cancellationToken);
                _logger.LogDebug("Published domain event: {EventType} at {OccurredAt}", 
                    domainEvent.GetType().Name, domainEvent.OccurredAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish domain event: {EventType}", 
                    domainEvent.GetType().Name);
                // Continue publishing other events even if one fails
            }
        }

        // Clear events after publishing
        entity.ClearDomainEvents();
    }

    /// <summary>
    /// Publishes all domain events from multiple entities.
    /// </summary>
    public async Task PublishEventsAsync(IEnumerable<BaseEntity> entities, CancellationToken cancellationToken = default)
    {
        if (entities == null)
            return;

        var entitiesArray = entities.ToArray();
        
        if (!entitiesArray.Any())
            return;

        var totalEvents = entitiesArray.Sum(e => e.DomainEvents.Count);
        
        if (totalEvents == 0)
            return;

        _logger.LogDebug("Publishing domain events from {EntityCount} entities ({EventCount} total events)", 
            entitiesArray.Length, totalEvents);

        foreach (var entity in entitiesArray)
        {
            await PublishEventsAsync(entity, cancellationToken);
        }
    }
}