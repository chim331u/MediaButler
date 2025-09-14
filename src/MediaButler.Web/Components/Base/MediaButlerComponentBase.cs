using Microsoft.AspNetCore.Components;

namespace MediaButler.Web.Components.Base;

/// <summary>
/// Base component class providing common functionality for MediaButler components.
/// Follows "Simple Made Easy" principles with clear separation of concerns.
/// </summary>
public abstract class MediaButlerComponentBase : ComponentBase, IDisposable
{
    [Inject] protected ILogger<MediaButlerComponentBase> Logger { get; set; } = default!;
    
    protected bool IsLoading { get; set; }
    protected string? ErrorMessage { get; set; }
    protected bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    protected CancellationToken CancellationToken => _cancellationTokenSource.Token;

    protected void SetLoading(bool loading)
    {
        if (IsLoading != loading)
        {
            IsLoading = loading;
            StateHasChanged();
        }
    }

    protected void SetError(string? error)
    {
        if (ErrorMessage != error)
        {
            ErrorMessage = error;
            StateHasChanged();
        }
    }

    protected void ClearError()
    {
        SetError(null);
    }

    protected async Task ExecuteAsync(Func<Task> operation, string? operationName = null)
    {
        try
        {
            ClearError();
            SetLoading(true);
            
            await operation();
        }
        catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
        {
            // Expected cancellation, don't treat as error
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error {operationName ?? "performing operation"}: {ex.Message}";
            Logger.LogError(ex, errorMsg);
            SetError(errorMsg);
        }
        finally
        {
            SetLoading(false);
        }
    }

    protected async Task<T?> ExecuteAsync<T>(Func<Task<T>> operation, string? operationName = null)
    {
        try
        {
            ClearError();
            SetLoading(true);
            
            return await operation();
        }
        catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested)
        {
            return default;
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error {operationName ?? "performing operation"}: {ex.Message}";
            Logger.LogError(ex, errorMsg);
            SetError(errorMsg);
            return default;
        }
        finally
        {
            SetLoading(false);
        }
    }

    public virtual void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }
}