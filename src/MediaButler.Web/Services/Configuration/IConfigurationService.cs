using MediaButler.Web.Models.Configuration;

namespace MediaButler.Web.Services.Configuration;

/// <summary>
/// Service interface for configuration management in the web application.
/// Provides comprehensive configuration operations following "Simple Made Easy" principles.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets all configuration sections with their settings.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of configuration sections organized for UI display</returns>
    Task<List<ConfigurationSectionModel>> GetAllSectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets configuration settings for a specific section.
    /// </summary>
    /// <param name="sectionName">Section name to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Configuration section with settings</returns>
    Task<ConfigurationSectionModel?> GetSectionAsync(string sectionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific configuration setting by key.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Configuration setting or null if not found</returns>
    Task<ConfigurationSettingModel?> GetSettingAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a configuration setting value.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="value">New value</param>
    /// <param name="description">Optional description</param>
    /// <param name="requiresRestart">Whether restart is required</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated configuration setting</returns>
    Task<ConfigurationSettingModel?> UpdateSettingAsync(string key, object value, string? description = null, bool requiresRestart = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new configuration setting.
    /// </summary>
    /// <param name="request">Configuration creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created configuration setting</returns>
    Task<ConfigurationSettingModel?> CreateSettingAsync(CreateConfigurationRequestModel request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a configuration setting.
    /// </summary>
    /// <param name="key">Configuration key to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteSettingAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets a configuration setting to its default value.
    /// </summary>
    /// <param name="key">Configuration key to reset</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Reset configuration setting</returns>
    Task<ConfigurationSettingModel?> ResetSettingAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches configuration settings by pattern.
    /// </summary>
    /// <param name="searchModel">Search criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Matching configuration settings</returns>
    Task<List<ConfigurationSettingModel>> SearchSettingsAsync(ConfigurationSearchModel searchModel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets configuration settings that require restart.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Settings requiring restart</returns>
    Task<List<ConfigurationSettingModel>> GetRestartRequiredSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recently modified configuration settings.
    /// </summary>
    /// <param name="hours">Hours to look back</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Recently modified settings</returns>
    Task<List<ConfigurationSettingModel>> GetRecentlyModifiedSettingsAsync(int hours = 24, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports configuration settings.
    /// </summary>
    /// <param name="exportModel">Export criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exported configuration data</returns>
    Task<string> ExportConfigurationAsync(ConfigurationExportModel exportModel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a configuration value against its rules.
    /// </summary>
    /// <param name="setting">Configuration setting to validate</param>
    /// <param name="value">Value to validate</param>
    /// <returns>Validation result with error message if invalid</returns>
    Task<(bool IsValid, string? ErrorMessage)> ValidateValueAsync(ConfigurationSettingModel setting, object value);

    /// <summary>
    /// Gets available configuration sections with metadata.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Available section names and descriptions</returns>
    Task<List<(string Name, string Title, string Description, string Icon)>> GetAvailableSectionsAsync(CancellationToken cancellationToken = default);
}