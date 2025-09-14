namespace MediaButler.Web.Services.Lifecycle;

/// <summary>
/// Component lifecycle management following "Simple Made Easy" principles.
/// Manages component state without complecting lifecycle with business logic.
/// </summary>
public interface IComponentLifecycleService
{
    void RegisterComponent(string componentId, IDisposable component);
    void UnregisterComponent(string componentId);
    Task DisposeAllAsync();
    bool IsRegistered(string componentId);
}

public class ComponentLifecycleService : IComponentLifecycleService, IDisposable
{
    private readonly Dictionary<string, IDisposable> _components = new();
    private readonly object _lock = new();
    private bool _disposed;

    public void RegisterComponent(string componentId, IDisposable component)
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_components.TryGetValue(componentId, out var existing))
            {
                existing.Dispose();
            }
            
            _components[componentId] = component;
        }
    }

    public void UnregisterComponent(string componentId)
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_components.TryGetValue(componentId, out var component))
            {
                component.Dispose();
                _components.Remove(componentId);
            }
        }
    }

    public bool IsRegistered(string componentId)
    {
        lock (_lock)
        {
            return _components.ContainsKey(componentId);
        }
    }

    public async Task DisposeAllAsync()
    {
        if (_disposed) return;

        IDisposable[] componentsToDispose;
        
        lock (_lock)
        {
            componentsToDispose = _components.Values.ToArray();
            _components.Clear();
        }

        var disposeTasks = componentsToDispose
            .Select(async component =>
            {
                try
                {
                    if (component is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync();
                    }
                    else
                    {
                        component.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing component: {ex.Message}");
                }
            });

        await Task.WhenAll(disposeTasks);
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        DisposeAllAsync().Wait();
    }
}