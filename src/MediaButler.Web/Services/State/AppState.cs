namespace MediaButler.Web.Services.State;

/// <summary>
/// Simple, immutable state management following "Simple Made Easy" principles.
/// Values over state - each state change produces a new immutable value.
/// </summary>
public record AppState
{
    public static readonly AppState Initial = new();
    
    public bool IsConnected { get; init; } = true;
    public string? LastError { get; init; }
    public DateTime? LastUpdated { get; init; }
    public int TotalFiles { get; init; }
    public int PendingFiles { get; init; }
    public int ProcessedToday { get; init; }
}

/// <summary>
/// State change events following functional programming principles.
/// Each event represents a pure transformation of state.
/// </summary>
public abstract record StateEvent
{
    public record ConnectionChanged(bool IsConnected) : StateEvent;
    public record ErrorOccurred(string Error) : StateEvent;
    public record ErrorCleared : StateEvent;
    public record StatisticsUpdated(int TotalFiles, int PendingFiles, int ProcessedToday) : StateEvent;
}

/// <summary>
/// Pure state reducer following Redux pattern.
/// No side effects, just state transformations.
/// </summary>
public static class StateReducer
{
    public static AppState Reduce(AppState state, StateEvent eventData)
    {
        return eventData switch
        {
            StateEvent.ConnectionChanged(var isConnected) => state with 
            { 
                IsConnected = isConnected, 
                LastUpdated = DateTime.UtcNow 
            },
            
            StateEvent.ErrorOccurred(var error) => state with 
            { 
                LastError = error, 
                LastUpdated = DateTime.UtcNow 
            },
            
            StateEvent.ErrorCleared => state with 
            { 
                LastError = null, 
                LastUpdated = DateTime.UtcNow 
            },
            
            StateEvent.StatisticsUpdated(var total, var pending, var today) => state with
            {
                TotalFiles = total,
                PendingFiles = pending,
                ProcessedToday = today,
                LastUpdated = DateTime.UtcNow
            },
            
            _ => state
        };
    }
}