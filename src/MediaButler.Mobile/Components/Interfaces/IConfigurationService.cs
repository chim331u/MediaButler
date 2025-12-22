namespace MediaButler.Mobile.Components.Interfaces;

/// <summary>
/// Configuration service for secure settings access.
/// Single responsibility: Configuration retrieval and validation only.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets the validated API base URL.
    /// Throws InvalidOperationException if configuration is invalid.
    /// </summary>
    string ApiBaseUrl { get; }

    /// <summary>
    /// Gets the API timeout in seconds.
    /// Default: 30 seconds.
    /// </summary>
    int ApiTimeout { get; }

    /// <summary>
    /// Gets whether response caching is enabled.
    /// Default: false.
    /// </summary>
    bool EnableCaching { get; }

    /// <summary>
    /// Indicates whether the configuration is valid and ready to use.
    /// </summary>
    bool IsValidConfiguration { get; }
}
