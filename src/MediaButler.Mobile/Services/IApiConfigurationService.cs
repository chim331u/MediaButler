using MediaButler.Mobile.Models;

namespace MediaButler.Mobile.Services;

/// <summary>
/// Service for managing API endpoint configuration.
/// Handles loading, saving, and updating configuration from JSON file.
/// </summary>
public interface IApiConfigurationService
{
    /// <summary>
    /// Event raised when configuration is reloaded.
    /// Subscribers should revalidate their connections.
    /// </summary>
    event EventHandler<ApiConfiguration>? ConfigurationChanged;

    /// <summary>
    /// Loads the configuration from the JSON file.
    /// Creates default configuration if file doesn't exist.
    /// </summary>
    Task<ApiConfiguration> LoadConfigurationAsync();

    /// <summary>
    /// Saves the configuration to the JSON file.
    /// </summary>
    Task SaveConfigurationAsync(ApiConfiguration configuration);

    /// <summary>
    /// Reloads the configuration from disk and raises ConfigurationChanged event.
    /// </summary>
    Task ReloadConfigurationAsync();

    /// <summary>
    /// Adds a new endpoint to the configuration.
    /// </summary>
    Task AddEndpointAsync(ApiEndpoint endpoint);

    /// <summary>
    /// Removes an endpoint from the configuration by name.
    /// </summary>
    Task RemoveEndpointAsync(string endpointName);

    /// <summary>
    /// Updates an existing endpoint.
    /// </summary>
    Task UpdateEndpointAsync(string endpointName, ApiEndpoint updatedEndpoint);

    /// <summary>
    /// Gets the current configuration (cached in memory).
    /// </summary>
    ApiConfiguration GetCurrentConfiguration();

    /// <summary>
    /// Checks if configuration file exists (indicates first run if false).
    /// </summary>
    Task<bool> HasConfigurationAsync();
}
