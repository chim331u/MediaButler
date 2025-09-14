namespace MediaButler.Web.Services.Events;

/// <summary>
/// Simple event bus for component communication following "Simple Made Easy" principles.
/// Decouples components without creating complex messaging infrastructure.
/// </summary>
public interface IEventBus
{
    void Subscribe<T>(Action<T> handler) where T : class;
    void Unsubscribe<T>(Action<T> handler) where T : class;
    void Publish<T>(T eventData) where T : class;
}

public class EventBus : IEventBus
{
    private readonly Dictionary<Type, List<object>> _handlers = new();
    private readonly object _lock = new();

    public void Subscribe<T>(Action<T> handler) where T : class
    {
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(T), out var handlers))
            {
                handlers = new List<object>();
                _handlers[typeof(T)] = handlers;
            }
            
            handlers.Add(handler);
        }
    }

    public void Unsubscribe<T>(Action<T> handler) where T : class
    {
        lock (_lock)
        {
            if (_handlers.TryGetValue(typeof(T), out var handlers))
            {
                handlers.Remove(handler);
                
                if (handlers.Count == 0)
                {
                    _handlers.Remove(typeof(T));
                }
            }
        }
    }

    public void Publish<T>(T eventData) where T : class
    {
        List<object> currentHandlers;
        
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(T), out var handlers))
                return;
                
            currentHandlers = new List<object>(handlers);
        }

        foreach (var handler in currentHandlers.Cast<Action<T>>())
        {
            try
            {
                handler(eventData);
            }
            catch (Exception ex)
            {
                // Log error but don't break other handlers
                Console.WriteLine($"Error in event handler: {ex.Message}");
            }
        }
    }
}