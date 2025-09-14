using Microsoft.AspNetCore.Components;

namespace MediaButler.Web.Components.Base;

/// <summary>
/// Base component for data-driven components that load and manage data.
/// Provides consistent loading, error handling, and refresh patterns.
/// </summary>
/// <typeparam name="T">The type of data this component manages</typeparam>
public abstract class DataComponentBase<T> : MediaButlerComponentBase where T : class
{
    protected T? Data { get; set; }
    protected bool HasData => Data != null;
    
    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    protected virtual async Task LoadDataAsync()
    {
        await ExecuteAsync(async () =>
        {
            Data = await FetchDataAsync();
        }, "loading data");
    }

    protected async Task RefreshDataAsync()
    {
        await LoadDataAsync();
    }

    protected abstract Task<T?> FetchDataAsync();
}