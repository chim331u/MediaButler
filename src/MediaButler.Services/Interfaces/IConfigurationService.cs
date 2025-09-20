using MediaButler.Core.Common;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;

namespace MediaButler.Services.Interfaces;

/// <summary>
/// Service interface for configuration management in the MediaButler system.
/// Provides type-safe access to dynamic application settings following "Simple Made Easy" principles
/// by keeping configuration concerns separate from business logic.
/// </summary>
/// <remarks>
/// This service manages application configuration settings including:
/// - ML model parameters (confidence thresholds, training intervals)
/// - File system paths (watch folders, media library locations)
/// - Processing settings (retry limits, batch sizes)
/// - System behaviors (auto-organize flags, notification preferences)
/// 
/// All configuration values are stored as JSON for consistency and type-safe access
/// is provided through generic methods. Configuration changes can be validated
/// before persistence and may require application restart for certain settings.
/// </remarks>
public interface IConfigurationService
{
    /// <summary>
    /// Retrieves a configuration value by key, deserializing it to the specified type.
    /// </summary>
    /// <typeparam name="T">The expected type of the configuration value.</typeparam>
    /// <param name="key">The configuration key in format 'Section.Key'.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing the typed configuration value if found and valid.</returns>
    Task<Result<T>> GetConfigurationAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a configuration value with a default fallback if the key is not found or invalid.
    /// </summary>
    /// <typeparam name="T">The expected type of the configuration value.</typeparam>
    /// <param name="key">The configuration key in format 'Section.Key'.</param>
    /// <param name="defaultValue">The default value to return if key is not found.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing the typed configuration value or default value.</returns>
    Task<Result<T>> GetConfigurationOrDefaultAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a configuration value, serializing it to JSON for consistent storage.
    /// </summary>
    /// <typeparam name="T">The type of the value to store.</typeparam>
    /// <param name="key">The configuration key in format 'Section.Key'.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="description">Optional description of the setting's purpose.</param>
    /// <param name="requiresRestart">Whether changing this setting requires application restart.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing the updated configuration setting if successful.</returns>
    Task<Result<ConfigurationSetting>> SetConfigurationAsync<T>(
        string key,
        T value,
        string section = "General",
        string? description = null,
        bool requiresRestart = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a configuration setting by key.
    /// </summary>
    /// <param name="key">The configuration key to remove.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result indicating success or failure of the removal.</returns>
    Task<Result> RemoveConfigurationAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all configuration settings for a specific section.
    /// </summary>
    /// <param name="section">The section name (e.g., 'ML', 'Paths', 'System').</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing all settings in the specified section.</returns>
    Task<Result<IEnumerable<ConfigurationSetting>>> GetSectionAsync(string section, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all configuration settings that require application restart.
    /// Used for deployment planning and change management.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing settings that require restart.</returns>
    Task<Result<IEnumerable<ConfigurationSetting>>> GetRestartRequiredSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a configuration value is valid for its expected data type.
    /// </summary>
    /// <param name="key">The configuration key to validate.</param>
    /// <param name="value">The value to validate (as JSON string).</param>
    /// <param name="dataType">The expected data type.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result indicating whether the value is valid for the data type.</returns>
    Task<Result<bool>> ValidateConfigurationValueAsync(string key, string value, ConfigurationDataType dataType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets a configuration setting to its system default value.
    /// </summary>
    /// <param name="key">The configuration key to reset.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing the reset configuration setting.</returns>
    Task<Result<ConfigurationSetting>> ResetToDefaultAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports all configuration settings to a JSON structure for backup or migration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing JSON representation of all settings.</returns>
    Task<Result<string>> ExportConfigurationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports configuration settings from a JSON structure, validating each setting.
    /// </summary>
    /// <param name="jsonConfiguration">JSON string containing configuration settings.</param>
    /// <param name="overwriteExisting">Whether to overwrite existing settings (default: false).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing import summary with success/failure counts.</returns>
    Task<Result<ConfigurationImportResult>> ImportConfigurationAsync(string jsonConfiguration, bool overwriteExisting = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets configuration settings that have been recently modified.
    /// Useful for monitoring configuration changes and audit purposes.
    /// </summary>
    /// <param name="withinHours">Number of hours to look back (default: 24).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing recently modified settings.</returns>
    Task<Result<IEnumerable<ConfigurationSetting>>> GetRecentlyModifiedAsync(int withinHours = 24, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for configuration settings by key pattern.
    /// Supports wildcards for flexible configuration discovery.
    /// </summary>
    /// <param name="keyPattern">Key pattern with wildcards (% and _).</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing matching configuration settings.</returns>
    Task<Result<IEnumerable<ConfigurationSetting>>> SearchConfigurationAsync(string keyPattern, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active configuration settings (IsActive = true).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing all active configuration settings.</returns>
    Task<Result<IEnumerable<ConfigurationSetting>>> GetActiveConfigurationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a configuration key exists in the system.
    /// </summary>
    /// <param name="key">The configuration key to check.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing true if the key exists, false otherwise.</returns>
    Task<Result<bool>> ConfigurationExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available configuration sections with their settings count.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result containing dictionary of sections and their setting counts.</returns>
    Task<Result<Dictionary<string, int>>> GetSectionSummaryAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result type for configuration import operations.
/// Provides detailed information about the import process.
/// </summary>
public class ConfigurationImportResult
{
    /// <summary>
    /// Gets or sets the number of settings successfully imported.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Gets or sets the number of settings that failed to import.
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Gets or sets the number of existing settings that were skipped.
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// Gets or sets the list of keys that failed to import with their error messages.
    /// </summary>
    public Dictionary<string, string> FailedKeys { get; set; } = new();

    /// <summary>
    /// Gets a value indicating whether the import was completely successful.
    /// </summary>
    public bool IsSuccessful => FailureCount == 0;

    /// <summary>
    /// Gets the total number of settings processed during import.
    /// </summary>
    public int TotalProcessed => SuccessCount + FailureCount + SkippedCount;
}