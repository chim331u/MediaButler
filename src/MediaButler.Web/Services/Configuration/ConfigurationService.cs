using MediaButler.Web.Models.Configuration;
using MediaButler.Web.Services;
using MediaButler.Core.Enums;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MediaButler.Web.Services.Configuration;

/// <summary>
/// Configuration management service implementation.
/// Handles communication with the configuration API and provides UI-specific functionality.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<ConfigurationService> _logger;

    public ConfigurationService(IApiClient apiClient, ILogger<ConfigurationService> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<ConfigurationSectionModel>> GetAllSectionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var availableSections = await GetAvailableSectionsAsync(cancellationToken);
            var sections = new List<ConfigurationSectionModel>();

            foreach (var (name, title, description, icon) in availableSections)
            {
                var section = await GetSectionAsync(name, cancellationToken);
                if (section != null)
                {
                    section.Title = title;
                    section.Description = description;
                    section.Icon = icon;
                    sections.Add(section);
                }
            }

            return sections.OrderBy(s => s.Order).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all configuration sections");
            return new List<ConfigurationSectionModel>();
        }
    }

    public async Task<ConfigurationSectionModel?> GetSectionAsync(string sectionName, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.GetAsync($"config/sections/{sectionName}", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var settings = JsonSerializer.Deserialize<List<ConfigurationSettingResponse>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (settings == null) return null;

            var section = new ConfigurationSectionModel
            {
                Name = sectionName,
                Title = GetSectionTitle(sectionName),
                Description = GetSectionDescription(sectionName),
                Icon = GetSectionIcon(sectionName),
                Order = GetSectionOrder(sectionName),
                Settings = settings.Select(ToConfigurationSettingModel).ToList()
            };

            return section;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get configuration section: {SectionName}", sectionName);
            return null;
        }
    }

    public async Task<ConfigurationSettingModel?> GetSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.GetAsync($"config/settings/{key}", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<dynamic>(content);
            
            // Simple mapping for single setting response
            return new ConfigurationSettingModel
            {
                Key = key,
                Value = apiResponse?.GetProperty("value"),
                Section = key.Split('.')[0],
                RawValue = apiResponse?.GetProperty("value")?.ToString() ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get configuration setting: {Key}", key);
            return null;
        }
    }

    public async Task<ConfigurationSettingModel?> UpdateSettingAsync(string key, object value, string? description = null, bool requiresRestart = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new UpdateConfigurationRequestModel
            {
                Value = value,
                Description = description,
                RequiresRestart = requiresRestart
            };

            var json = JsonSerializer.Serialize(request);
            var response = await _apiClient.PutAsJsonAsync($"config/settings/{key}", request, cancellationToken);
            
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var setting = JsonSerializer.Deserialize<ConfigurationSettingResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return setting != null ? ToConfigurationSettingModel(setting) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update configuration setting: {Key}", key);
            return null;
        }
    }

    public async Task<ConfigurationSettingModel?> CreateSettingAsync(CreateConfigurationRequestModel request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.PostAsJsonAsync("config/settings", request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var setting = JsonSerializer.Deserialize<ConfigurationSettingResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return setting != null ? ToConfigurationSettingModel(setting) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create configuration setting: {Key}", request.Key);
            return null;
        }
    }

    public async Task<bool> DeleteSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.DeleteAsync($"config/settings/{key}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete configuration setting: {Key}", key);
            return false;
        }
    }

    public async Task<ConfigurationSettingModel?> ResetSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.PostAsync($"config/settings/{key}/reset", null, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var setting = JsonSerializer.Deserialize<ConfigurationSettingResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return setting != null ? ToConfigurationSettingModel(setting) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset configuration setting: {Key}", key);
            return null;
        }
    }

    public async Task<List<ConfigurationSettingModel>> SearchSettingsAsync(ConfigurationSearchModel searchModel, CancellationToken cancellationToken = default)
    {
        try
        {
            var pattern = string.IsNullOrEmpty(searchModel.SearchTerm) ? "*" : $"*{searchModel.SearchTerm}*";
            var response = await _apiClient.GetAsync($"config/search?pattern={pattern}", cancellationToken);
            
            if (!response.IsSuccessStatusCode) return new List<ConfigurationSettingModel>();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var settings = JsonSerializer.Deserialize<List<ConfigurationSettingResponse>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (settings == null) return new List<ConfigurationSettingModel>();

            var results = settings.Select(ToConfigurationSettingModel).ToList();

            // Apply client-side filters
            if (!string.IsNullOrEmpty(searchModel.Section))
            {
                results = results.Where(s => s.Section.Equals(searchModel.Section, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (searchModel.ModifiedOnly)
            {
                results = results.Where(s => s.IsModified).ToList();
            }

            if (searchModel.RestartRequiredOnly)
            {
                results = results.Where(s => s.RequiresRestart).ToList();
            }

            if (searchModel.DataType.HasValue)
            {
                results = results.Where(s => s.DataType == searchModel.DataType.Value).ToList();
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search configuration settings");
            return new List<ConfigurationSettingModel>();
        }
    }

    public async Task<List<ConfigurationSettingModel>> GetRestartRequiredSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.GetAsync("config/restart-required", cancellationToken);
            if (!response.IsSuccessStatusCode) return new List<ConfigurationSettingModel>();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var settings = JsonSerializer.Deserialize<List<ConfigurationSettingResponse>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return settings?.Select(ToConfigurationSettingModel).ToList() ?? new List<ConfigurationSettingModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get restart required settings");
            return new List<ConfigurationSettingModel>();
        }
    }

    public async Task<List<ConfigurationSettingModel>> GetRecentlyModifiedSettingsAsync(int hours = 24, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.GetAsync($"config/recent?hours={hours}", cancellationToken);
            if (!response.IsSuccessStatusCode) return new List<ConfigurationSettingModel>();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var settings = JsonSerializer.Deserialize<List<ConfigurationSettingResponse>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return settings?.Select(ToConfigurationSettingModel).ToList() ?? new List<ConfigurationSettingModel>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recently modified settings");
            return new List<ConfigurationSettingModel>();
        }
    }

    public async Task<string> ExportConfigurationAsync(ConfigurationExportModel exportModel, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _apiClient.GetAsync("config/export", cancellationToken);
            if (!response.IsSuccessStatusCode) return string.Empty;

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export configuration");
            return string.Empty;
        }
    }

    public async Task<(bool IsValid, string? ErrorMessage)> ValidateValueAsync(ConfigurationSettingModel setting, object value)
    {
        try
        {
            // Basic validation based on data type
            switch (setting.DataType)
            {
                case ConfigurationDataType.Boolean:
                    if (value is not bool && !bool.TryParse(value.ToString(), out _))
                        return (false, "Value must be true or false");
                    break;

                case ConfigurationDataType.Integer:
                    if (value is not int && !int.TryParse(value.ToString(), out _))
                        return (false, "Value must be a valid integer");
                    break;

                case ConfigurationDataType.Path:
                    var pathValue = value.ToString();
                    if (string.IsNullOrWhiteSpace(pathValue))
                        return (false, "Path cannot be empty");
                    
                    // Basic path validation
                    var invalidChars = Path.GetInvalidPathChars();
                    if (pathValue.IndexOfAny(invalidChars) >= 0)
                        return (false, "Path contains invalid characters");
                    break;

                case ConfigurationDataType.Json:
                    try
                    {
                        JsonSerializer.Deserialize<object>(value.ToString() ?? "{}");
                    }
                    catch
                    {
                        return (false, "Value must be valid JSON");
                    }
                    break;
            }

            // Apply validation rules
            foreach (var rule in setting.ValidationRules)
            {
                var result = await ValidateRule(rule, value);
                if (!result.IsValid)
                    return result;
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate configuration value");
            return (false, "Validation failed due to an error");
        }
    }

    public async Task<List<(string Name, string Title, string Description, string Icon)>> GetAvailableSectionsAsync(CancellationToken cancellationToken = default)
    {
        // Return predefined sections - could be made dynamic later
        return new List<(string Name, string Title, string Description, string Icon)>
        {
            ("Paths", "File Paths", "Configure watch folders, media library paths, and file locations", "folder"),
            ("ML", "Machine Learning", "Settings for ML classification engine and confidence thresholds", "brain"),
            ("Processing", "File Processing", "Configuration for file operations, concurrency, and performance", "gear"),
            ("Monitoring", "System Monitoring", "Logging, performance monitoring, and health check settings", "monitor"),
            ("Security", "Security Settings", "Authentication, authorization, and security policies", "shield"),
            ("UI", "User Interface", "Web interface preferences and display options", "layout"),
            ("Performance", "Performance Tuning", "Memory limits, caching, and performance optimization", "gauge")
        };
    }

    private static ConfigurationSettingModel ToConfigurationSettingModel(ConfigurationSettingResponse response)
    {
        return new ConfigurationSettingModel
        {
            Key = response.Key,
            Value = response.Value,
            RawValue = response.RawValue,
            Section = response.Section,
            Description = response.Description,
            DataType = (ConfigurationDataType)response.DataType,
            DataTypeDescription = response.DataTypeDescription,
            RequiresRestart = response.RequiresRestart,
            IsEditable = response.IsEditable,
            DefaultValue = response.DefaultValue,
            CreatedAt = response.CreatedAt,
            UpdatedAt = response.UpdatedAt,
            ValidationRules = response.ValidationRules?.Select(r => new ValidationRuleModel
            {
                Type = r.Type,
                Parameters = r.Parameters,
                ErrorMessage = r.ErrorMessage
            }).ToList() ?? new List<ValidationRuleModel>(),
            Options = response.Options?.Select(o => new ConfigurationOptionModel
            {
                Value = o.Value,
                Label = o.Label,
                Description = o.Description,
                IsDeprecated = o.IsDeprecated
            }).ToList()
        };
    }

    private async Task<(bool IsValid, string? ErrorMessage)> ValidateRule(ValidationRuleModel rule, object value)
    {
        return rule.Type.ToLower() switch
        {
            "range" when rule.Parameters.ContainsKey("min") && rule.Parameters.ContainsKey("max") => 
                await ValidateRange(value, rule.Parameters["min"], rule.Parameters["max"], rule.ErrorMessage),
            "pattern" when rule.Parameters.ContainsKey("regex") => 
                ValidatePattern(value, rule.Parameters["regex"].ToString(), rule.ErrorMessage),
            "required" => ValidateRequired(value, rule.ErrorMessage),
            _ => (true, null)
        };
    }

    private static async Task<(bool IsValid, string? ErrorMessage)> ValidateRange(object value, object min, object max, string errorMessage)
    {
        try
        {
            if (value is IComparable comparableValue && min is IComparable minValue && max is IComparable maxValue)
            {
                if (comparableValue.CompareTo(minValue) < 0 || comparableValue.CompareTo(maxValue) > 0)
                    return (false, errorMessage);
            }
            return (true, null);
        }
        catch
        {
            return (false, errorMessage);
        }
    }

    private static (bool IsValid, string? ErrorMessage) ValidatePattern(object value, string? pattern, string errorMessage)
    {
        if (pattern == null) return (true, null);
        
        try
        {
            var regex = new Regex(pattern);
            if (!regex.IsMatch(value.ToString() ?? string.Empty))
                return (false, errorMessage);
            return (true, null);
        }
        catch
        {
            return (false, errorMessage);
        }
    }

    private static (bool IsValid, string? ErrorMessage) ValidateRequired(object value, string errorMessage)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            return (false, errorMessage);
        return (true, null);
    }

    private static string GetSectionTitle(string sectionName) => sectionName switch
    {
        "Paths" => "File Paths",
        "ML" => "Machine Learning",
        "Processing" => "File Processing",
        "Monitoring" => "System Monitoring",
        "Security" => "Security Settings",
        "UI" => "User Interface",
        "Performance" => "Performance Tuning",
        _ => sectionName
    };

    private static string GetSectionDescription(string sectionName) => sectionName switch
    {
        "Paths" => "Configure watch folders, media library paths, and file locations",
        "ML" => "Settings for ML classification engine and confidence thresholds",
        "Processing" => "Configuration for file operations, concurrency, and performance",
        "Monitoring" => "Logging, performance monitoring, and health check settings",
        "Security" => "Authentication, authorization, and security policies",
        "UI" => "Web interface preferences and display options",
        "Performance" => "Memory limits, caching, and performance optimization",
        _ => $"Settings for {sectionName}"
    };

    private static string GetSectionIcon(string sectionName) => sectionName switch
    {
        "Paths" => "folder",
        "ML" => "brain",
        "Processing" => "gear",
        "Monitoring" => "monitor",
        "Security" => "shield",
        "UI" => "layout",
        "Performance" => "gauge",
        _ => "settings"
    };

    private static int GetSectionOrder(string sectionName) => sectionName switch
    {
        "Paths" => 1,
        "ML" => 2,
        "Processing" => 3,
        "Performance" => 4,
        "Monitoring" => 5,
        "UI" => 6,
        "Security" => 7,
        _ => 99
    };
}

/// <summary>
/// API response model matching the backend ConfigurationResponse.
/// </summary>
internal class ConfigurationSettingResponse
{
    public required string Key { get; set; }
    public object? Value { get; set; }
    public string RawValue { get; set; } = string.Empty;
    public required string Section { get; set; }
    public string? Description { get; set; }
    public int DataType { get; set; }
    public string DataTypeDescription { get; set; } = string.Empty;
    public bool RequiresRestart { get; set; }
    public bool IsEditable { get; set; } = true;
    public object? DefaultValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ValidationRuleResponse>? ValidationRules { get; set; }
    public List<ConfigurationOptionResponse>? Options { get; set; }
}

internal class ValidationRuleResponse
{
    public required string Type { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public required string ErrorMessage { get; set; }
}

internal class ConfigurationOptionResponse
{
    public required object Value { get; set; }
    public required string Label { get; set; }
    public string? Description { get; set; }
    public bool IsDeprecated { get; set; }
}