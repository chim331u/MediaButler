using System.Text.Json;
using MediaButler.Core.Common;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Data.UnitOfWork;
using MediaButler.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services;

/// <summary>
/// Service implementation for configuration management in the MediaButler system.
/// Provides type-safe access to dynamic application settings following "Simple Made Easy" principles.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ConfigurationService> _logger;

    /// <summary>
    /// System default configuration values.
    /// </summary>
    private static readonly Dictionary<string, (object Value, ConfigurationDataType DataType, string Section, string Description, bool RequiresRestart)> SystemDefaults = new()
    {
        ["ML.ConfidenceThreshold"] = (0.75m, ConfigurationDataType.Json, "ML", "Minimum confidence threshold for auto-classification", false),
        ["ML.TrainingIntervalHours"] = (24, ConfigurationDataType.Integer, "ML", "Hours between automatic model retraining", true),
        ["Paths.WatchFolder"] = ("/media/downloads", ConfigurationDataType.Path, "Paths", "Folder to monitor for new files", true),
        ["Paths.MediaLibrary"] = ("/media/library", ConfigurationDataType.Path, "Paths", "Target library path for organized files", true),
        ["Butler.ScanIntervalMinutes"] = (30, ConfigurationDataType.Integer, "Butler", "Minutes between folder scans", false),
        ["Butler.MaxRetryCount"] = (3, ConfigurationDataType.Integer, "Butler", "Maximum retry attempts for failed operations", false),
        ["System.LogLevel"] = ("Information", ConfigurationDataType.String, "System", "Minimum log level", true)
    };

    public ConfigurationService(IUnitOfWork unitOfWork, ILogger<ConfigurationService> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<T>> GetConfigurationAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result<T>.Failure("Configuration key cannot be empty");

        try
        {
            var setting = await GetConfigurationSettingByKeyAsync(key, cancellationToken);
            if (setting == null)
                return Result<T>.Failure($"Configuration key not found: {key}");

            var deserializedValue = DeserializeValue<T>(setting.Value, setting.DataType);
            return Result<T>.Success(deserializedValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get configuration: {Key}", key);
            return Result<T>.Failure($"Failed to retrieve configuration '{key}': {ex.Message}");
        }
    }

    public async Task<Result<T>> GetConfigurationOrDefaultAsync<T>(string key, T defaultValue, CancellationToken cancellationToken = default)
    {
        var result = await GetConfigurationAsync<T>(key, cancellationToken);
        
        if (result.IsSuccess)
            return result;

        _logger.LogDebug("Configuration key {Key} not found, returning default value", key);
        return Result<T>.Success(defaultValue);
    }

    public async Task<Result<ConfigurationSetting>> SetConfigurationAsync<T>(
        string key, 
        T value, 
        string? description = null, 
        bool requiresRestart = false, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result<ConfigurationSetting>.Failure("Configuration key cannot be empty");

        if (value == null)
            return Result<ConfigurationSetting>.Failure("Configuration value cannot be null");

        try
        {
            var jsonValue = SerializeValue(value);
            var dataType = DetermineDataType<T>();
            var section = ExtractSectionFromKey(key);

            var validationResult = await ValidateConfigurationValueAsync(key, jsonValue, dataType, cancellationToken);
            if (!validationResult.IsSuccess)
                return Result<ConfigurationSetting>.Failure($"Invalid configuration value: {validationResult.Error}");

            var existingSetting = await GetConfigurationSettingByKeyAsync(key, cancellationToken);
            
            if (existingSetting != null)
            {
                existingSetting.UpdateValue(jsonValue, $"Updated via ConfigurationService at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                
                if (!string.IsNullOrWhiteSpace(description))
                    existingSetting.Description = description;
                
                existingSetting.RequiresRestart = requiresRestart;
                existingSetting.DataType = dataType;
            }
            else
            {
                existingSetting = new ConfigurationSetting
                {
                    Key = key,
                    Value = jsonValue,
                    Section = section,
                    Description = description ?? $"Configuration setting for {key}",
                    DataType = dataType,
                    RequiresRestart = requiresRestart
                };

                _unitOfWork.ConfigurationSettings.Add(existingSetting);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Configuration '{Key}' updated successfully", key);
            return Result<ConfigurationSetting>.Success(existingSetting);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set configuration: {Key}", key);
            return Result<ConfigurationSetting>.Failure($"Failed to set configuration '{key}': {ex.Message}");
        }
    }

    public async Task<Result> RemoveConfigurationAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result.Failure("Configuration key cannot be empty");

        try
        {
            var setting = await GetConfigurationSettingByKeyAsync(key, cancellationToken);
            if (setting == null)
                return Result.Failure($"Configuration key not found: {key}");

            _unitOfWork.ConfigurationSettings.SoftDelete(setting);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Configuration '{Key}' removed successfully", key);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove configuration: {Key}", key);
            return Result.Failure($"Failed to remove configuration '{key}': {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<ConfigurationSetting>>> GetSectionAsync(string section, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(section))
            return Result<IEnumerable<ConfigurationSetting>>.Failure("Section name cannot be empty");

        try
        {
            var settings = await _unitOfWork.ConfigurationSettings.FindAsync(
                s => s.Section == section, cancellationToken);

            return Result<IEnumerable<ConfigurationSetting>>.Success(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get configuration section: {Section}", section);
            return Result<IEnumerable<ConfigurationSetting>>.Failure($"Failed to retrieve section '{section}': {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<ConfigurationSetting>>> GetRestartRequiredSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _unitOfWork.ConfigurationSettings.FindAsync(
                s => s.RequiresRestart, cancellationToken);

            return Result<IEnumerable<ConfigurationSetting>>.Success(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get restart-required settings");
            return Result<IEnumerable<ConfigurationSetting>>.Failure($"Failed to retrieve restart-required settings: {ex.Message}");
        }
    }

    public async Task<Result<bool>> ValidateConfigurationValueAsync(string key, string value, ConfigurationDataType dataType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result<bool>.Failure("Configuration key cannot be empty");

        if (string.IsNullOrWhiteSpace(value))
            return Result<bool>.Failure("Configuration value cannot be empty");

        try
        {
            switch (dataType)
            {
                case ConfigurationDataType.Integer:
                    if (!int.TryParse(value, out _))
                        return Result<bool>.Failure($"Value '{value}' is not a valid integer");
                    break;

                case ConfigurationDataType.Boolean:
                    if (!bool.TryParse(value, out _))
                        return Result<bool>.Failure($"Value '{value}' is not a valid boolean");
                    break;

                case ConfigurationDataType.Path:
                    var invalidChars = Path.GetInvalidPathChars();
                    if (value.IndexOfAny(invalidChars) >= 0)
                        return Result<bool>.Failure($"Value '{value}' contains invalid path characters");
                    break;

                case ConfigurationDataType.Json:
                    try
                    {
                        JsonDocument.Parse(value);
                    }
                    catch (JsonException ex)
                    {
                        return Result<bool>.Failure($"Value '{value}' is not valid JSON: {ex.Message}");
                    }
                    break;

                case ConfigurationDataType.String:
                    break;

                default:
                    return Result<bool>.Failure($"Unknown data type: {dataType}");
            }

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate configuration value: {Key}", key);
            return Result<bool>.Failure($"Validation failed for '{key}': {ex.Message}");
        }
    }

    public async Task<Result<ConfigurationSetting>> ResetToDefaultAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result<ConfigurationSetting>.Failure("Configuration key cannot be empty");

        if (!SystemDefaults.ContainsKey(key))
            return Result<ConfigurationSetting>.Failure($"No system default exists for configuration key: {key}");

        try
        {
            var defaultConfig = SystemDefaults[key];
            var result = await SetConfigurationAsync(
                key, 
                defaultConfig.Value, 
                defaultConfig.Description, 
                defaultConfig.RequiresRestart, 
                cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Configuration '{Key}' reset to default value", key);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset configuration to default: {Key}", key);
            return Result<ConfigurationSetting>.Failure($"Failed to reset '{key}' to default: {ex.Message}");
        }
    }

    public async Task<Result<string>> ExportConfigurationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var allSettings = await _unitOfWork.ConfigurationSettings.GetAllAsync(cancellationToken);

            var exportData = allSettings.Select(s => new
            {
                s.Key,
                s.Value,
                s.Section,
                s.Description,
                DataType = s.DataType.ToString(),
                s.RequiresRestart,
                ExportedAt = DateTime.UtcNow
            });

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(exportData, jsonOptions);
            
            _logger.LogInformation("Configuration export completed with {Count} settings", allSettings.Count());
            return Result<string>.Success(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export configuration");
            return Result<string>.Failure($"Failed to export configuration: {ex.Message}");
        }
    }

    public async Task<Result<ConfigurationImportResult>> ImportConfigurationAsync(string jsonConfiguration, bool overwriteExisting = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jsonConfiguration))
            return Result<ConfigurationImportResult>.Failure("JSON configuration cannot be empty");

        var importResult = new ConfigurationImportResult();

        try
        {
            var importData = JsonSerializer.Deserialize<List<ImportConfigurationItem>>(jsonConfiguration);
            if (importData == null)
                return Result<ConfigurationImportResult>.Failure("Invalid JSON configuration format");

            foreach (var item in importData)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(item.Key) || string.IsNullOrWhiteSpace(item.Value))
                    {
                        importResult.FailedKeys[item.Key ?? "unknown"] = "Key and Value are required";
                        importResult.FailureCount++;
                        continue;
                    }

                    if (!Enum.TryParse<ConfigurationDataType>(item.DataType, out var dataType))
                    {
                        importResult.FailedKeys[item.Key] = $"Invalid data type: {item.DataType}";
                        importResult.FailureCount++;
                        continue;
                    }

                    var existingSetting = await GetConfigurationSettingByKeyAsync(item.Key, cancellationToken);
                    
                    if (existingSetting != null && !overwriteExisting)
                    {
                        importResult.SkippedCount++;
                        continue;
                    }

                    var validationResult = await ValidateConfigurationValueAsync(item.Key, item.Value, dataType, cancellationToken);
                    if (!validationResult.IsSuccess)
                    {
                        importResult.FailedKeys[item.Key] = validationResult.Error!;
                        importResult.FailureCount++;
                        continue;
                    }

                    var setResult = await SetConfigurationFromImport(item, cancellationToken);
                    if (setResult.IsSuccess)
                    {
                        importResult.SuccessCount++;
                    }
                    else
                    {
                        importResult.FailedKeys[item.Key] = setResult.Error!;
                        importResult.FailureCount++;
                    }
                }
                catch (Exception ex)
                {
                    importResult.FailedKeys[item.Key ?? "unknown"] = ex.Message;
                    importResult.FailureCount++;
                }
            }

            _logger.LogInformation("Configuration import completed: {Success} successful, {Failed} failed, {Skipped} skipped", 
                importResult.SuccessCount, importResult.FailureCount, importResult.SkippedCount);

            return Result<ConfigurationImportResult>.Success(importResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import configuration");
            return Result<ConfigurationImportResult>.Failure($"Failed to import configuration: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<ConfigurationSetting>>> GetRecentlyModifiedAsync(int withinHours = 24, CancellationToken cancellationToken = default)
    {
        if (withinHours <= 0)
            return Result<IEnumerable<ConfigurationSetting>>.Failure("Hours must be greater than 0");

        try
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-withinHours);
            
            var settings = await _unitOfWork.ConfigurationSettings.FindAsync(
                s => s.LastUpdateDate >= cutoffTime, cancellationToken);

            return Result<IEnumerable<ConfigurationSetting>>.Success(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recently modified configurations");
            return Result<IEnumerable<ConfigurationSetting>>.Failure($"Failed to retrieve recently modified settings: {ex.Message}");
        }
    }

    public async Task<Result<IEnumerable<ConfigurationSetting>>> SearchConfigurationAsync(string keyPattern, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyPattern))
            return Result<IEnumerable<ConfigurationSetting>>.Failure("Key pattern cannot be empty");

        try
        {
            var settings = await _unitOfWork.ConfigurationSettings.FindAsync(
                s => s.Key.Contains(keyPattern) || (s.Description != null && s.Description.Contains(keyPattern)), 
                cancellationToken);

            return Result<IEnumerable<ConfigurationSetting>>.Success(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search configurations with pattern: {Pattern}", keyPattern);
            return Result<IEnumerable<ConfigurationSetting>>.Failure($"Failed to search configurations: {ex.Message}");
        }
    }

    public async Task<Result<bool>> ConfigurationExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result<bool>.Failure("Configuration key cannot be empty");

        try
        {
            var setting = await GetConfigurationSettingByKeyAsync(key, cancellationToken);
            return Result<bool>.Success(setting != null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check configuration existence: {Key}", key);
            return Result<bool>.Failure($"Failed to check configuration existence for '{key}': {ex.Message}");
        }
    }

    public async Task<Result<Dictionary<string, int>>> GetSectionSummaryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var allSettings = await _unitOfWork.ConfigurationSettings.GetAllAsync(cancellationToken);

            var sectionSummary = allSettings
                .GroupBy(s => s.Section)
                .ToDictionary(g => g.Key, g => g.Count());

            return Result<Dictionary<string, int>>.Success(sectionSummary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get section summary");
            return Result<Dictionary<string, int>>.Failure($"Failed to get section summary: {ex.Message}");
        }
    }

    #region Private Helper Methods

    private async Task<ConfigurationSetting?> GetConfigurationSettingByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        var setting = await _unitOfWork.ConfigurationSettings.FirstOrDefaultAsync(
            s => s.Key == key, cancellationToken);

        return setting;
    }

    private static T DeserializeValue<T>(string jsonValue, ConfigurationDataType dataType)
    {
        try
        {
            return dataType switch
            {
                ConfigurationDataType.String => (T)(object)jsonValue.Trim('"'),
                ConfigurationDataType.Integer => (T)(object)int.Parse(jsonValue),
                ConfigurationDataType.Boolean => (T)(object)bool.Parse(jsonValue),
                ConfigurationDataType.Path => (T)(object)jsonValue.Trim('"'),
                ConfigurationDataType.Json => JsonSerializer.Deserialize<T>(jsonValue)!,
                _ => throw new ArgumentException($"Unsupported data type: {dataType}")
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to deserialize configuration value '{jsonValue}' as {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    private static string SerializeValue<T>(T value)
    {
        return value switch
        {
            string str => JsonSerializer.Serialize(str),
            int or long or float or double or decimal => value.ToString()!,
            bool boolean => boolean.ToString().ToLowerInvariant(),
            _ => JsonSerializer.Serialize(value)
        };
    }

    private static ConfigurationDataType DetermineDataType<T>()
    {
        var type = typeof(T);
        
        if (type == typeof(string))
            return ConfigurationDataType.String;
        
        if (type == typeof(int) || type == typeof(long))
            return ConfigurationDataType.Integer;
        
        if (type == typeof(bool))
            return ConfigurationDataType.Boolean;
        
        return ConfigurationDataType.Json;
    }

    private static string ExtractSectionFromKey(string key)
    {
        var dotIndex = key.IndexOf('.');
        return dotIndex > 0 ? key[..dotIndex] : "General";
    }

    private async Task<Result<ConfigurationSetting>> SetConfigurationFromImport(ImportConfigurationItem item, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ConfigurationDataType>(item.DataType, out var dataType))
            return Result<ConfigurationSetting>.Failure($"Invalid data type: {item.DataType}");

        try
        {
            object typedValue = dataType switch
            {
                ConfigurationDataType.String => item.Value.Trim('"'),
                ConfigurationDataType.Integer => int.Parse(item.Value),
                ConfigurationDataType.Boolean => bool.Parse(item.Value),
                ConfigurationDataType.Path => item.Value.Trim('"'),
                ConfigurationDataType.Json => JsonDocument.Parse(item.Value).RootElement,
                _ => item.Value
            };

            return await SetConfigurationAsync(
                item.Key, 
                typedValue, 
                item.Description, 
                item.RequiresRestart, 
                cancellationToken);
        }
        catch (Exception ex)
        {
            return Result<ConfigurationSetting>.Failure($"Failed to convert value: {ex.Message}");
        }
    }

    #endregion

    private class ImportConfigurationItem
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string DataType { get; set; } = string.Empty;
        public bool RequiresRestart { get; set; }
    }
}