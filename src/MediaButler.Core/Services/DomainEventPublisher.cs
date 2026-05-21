using MediaButler.Core.Common;
using MediaButler.Core.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace MediaButler.Core.Services;

/// <summary>
/// Service responsible for publishing domain events from entities.
/// Uses custom event dispatcher to decouple event publishers from handlers following "Simple Made Easy" principles.
/// Replaces MediatR with direct DI resolution to remove external dependencies.
/// </summary>
public class DomainEventPublisher : IDomainEventPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DomainEventPublisher> _logger;

    public DomainEventPublisher(IServiceProvider serviceProvider, ILogger<DomainEventPublisher> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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

        // Clear events before publishing to prevent infinite recursion on nested SaveChangesAsync
        entity.ClearDomainEvents();

        _logger.LogDebug("Publishing {EventCount} domain events for entity", domainEvents.Count);

        foreach (var domainEvent in domainEvents)
        {
            try
            {
                await PublishAsync(domainEvent, cancellationToken);
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
    }

    private async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var eventType = domainEvent.GetType();
        var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);
        var handlers = _serviceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            if (handler == null) continue;

            var method = handlerType.GetMethod("HandleAsync");
            if (method != null)
            {
                await (Task)method.Invoke(handler, new object[] { domainEvent, cancellationToken })!;
            }
        }
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