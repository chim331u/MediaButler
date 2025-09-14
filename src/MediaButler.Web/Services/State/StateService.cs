namespace MediaButler.Web.Services.State;

public interface IStateService
{
    AppState CurrentState { get; }
    event Action<AppState> StateChanged;
    void Dispatch(StateEvent eventData);
}

/// <summary>
/// Simple state service without complecting state management with business logic.
/// Pure functional approach with immutable state transitions.
/// </summary>
public class StateService : IStateService
{
    private AppState _currentState = AppState.Initial;
    
    public AppState CurrentState => _currentState;
    public event Action<AppState>? StateChanged;

    public void Dispatch(StateEvent eventData)
    {
        var newState = StateReducer.Reduce(_currentState, eventData);
        
        if (!ReferenceEquals(_currentState, newState))
        {
            _currentState = newState;
            StateChanged?.Invoke(_currentState);
        }
    }
}