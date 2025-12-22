namespace MediaButler.Mobile.Models;

/// <summary>
/// Feature flags for incremental migration rollout.
/// Enables gradual enablement of new API features without breaking changes.
/// </summary>
public class FeatureFlags
{
    /// <summary>
    /// Master switch: Route all API calls to new services.
    /// When false, uses legacy API.
    /// </summary>
    public bool UseNewApi { get; set; } = false;

    /// <summary>
    /// Enable batch file operations via new API.
    /// Requires UseNewApi = true.
    /// </summary>
    public bool EnableBatchOperations { get; set; } = false;

    /// <summary>
    /// Enable server-side pagination for file lists.
    /// Loads 20 files at a time instead of all files.
    /// </summary>
    public bool EnablePagination { get; set; } = false;

    /// <summary>
    /// Use centralized SignalR notification service.
    /// When false, uses component-level SignalR connections.
    /// </summary>
    public bool EnableSignalRService { get; set; } = false;

    /// <summary>
    /// Enable response caching for category lists (5 min TTL).
    /// Reduces API calls for frequently accessed data.
    /// </summary>
    public bool EnableCaching { get; set; } = false;
}
