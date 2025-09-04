using MediaButler.Core.Enums;
using System.Text.Json.Serialization;

namespace MediaButler.API.Models.Response;

/// <summary>
/// Data transfer object representing a configuration setting for API responses.
/// Maps domain ConfigurationSetting entity to API-appropriate format following "Simple Made Easy" principles.
/// Separates API concerns from domain entity structure.
/// </summary>
/// <remarks>
/// This DTO provides a stable API contract for configuration management that can evolve
/// independently of the domain model. It includes user-friendly formatting and validation
/// information to support client-side configuration interfaces.
/// </remarks>
public class ConfigurationResponse
{
    /// <summary>
    /// Gets or sets the unique identifier for this configuration setting.
    /// Used as the primary key for configuration management operations.
    /// </summary>
    /// <value>A unique string key identifying this setting.</value>
    /// <example>ML.ConfidenceThreshold</example>
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets the current value of this configuration setting.
    /// The value is presented in its typed format for client consumption.
    /// </summary>
    /// <value>The configuration value in its appropriate data type.</value>
    /// <example>0.85</example>
    public object? Value { get; set; }

    /// <summary>
    /// Gets or sets the raw string representation of the value.
    /// Useful for editing interfaces that need to display the exact stored value.
    /// </summary>
    /// <value>The value as stored in the database (JSON format).</value>
    /// <example>"0.85"</example>
    public string RawValue { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the logical section this setting belongs to.
    /// Used for organizing settings in the user interface.
    /// </summary>
    /// <value>The section name for grouping related settings.</value>
    /// <example>ML</example>
    public required string Section { get; set; }

    /// <summary>
    /// Gets or sets a human-readable description of this setting.
    /// Provides context for users about the setting's purpose and effects.
    /// </summary>
    /// <value>A descriptive explanation of the setting.</value>
    /// <example>Minimum confidence threshold for automatic file classification</example>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the data type of this setting's value.
    /// Used for client-side validation and input control selection.
    /// </summary>
    /// <value>A ConfigurationDataType enum indicating the expected value type.</value>
    /// <example>Integer</example>
    public ConfigurationDataType DataType { get; set; }

    /// <summary>
    /// Gets or sets the user-friendly data type description.
    /// Provides readable type information for display purposes.
    /// </summary>
    /// <value>A human-readable description of the data type.</value>
    /// <example>Decimal Number</example>
    public string DataTypeDescription { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether changing this setting requires an application restart.
    /// Important information for users to understand the impact of changes.
    /// </summary>
    /// <value>True if restart is required for changes to take effect.</value>
    /// <example>false</example>
    public bool RequiresRestart { get; set; }

    /// <summary>
    /// Gets or sets whether this setting can be modified via the API.
    /// Some settings may be read-only for security or operational reasons.
    /// </summary>
    /// <value>True if the setting can be modified through the API.</value>
    /// <example>true</example>
    public bool IsEditable { get; set; } = true;

    /// <summary>
    /// Gets or sets the default value for this setting.
    /// Useful for reset operations and showing what the original value was.
    /// </summary>
    /// <value>The default value for this setting, or null if no default is defined.</value>
    /// <example>0.8</example>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets when this setting was created.
    /// Provides audit trail information for configuration management.
    /// </summary>
    /// <value>The UTC date and time when this setting was first created.</value>
    /// <example>2024-01-15T10:30:00Z</example>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets when this setting was last updated.
    /// Shows the most recent modification timestamp.
    /// </summary>
    /// <value>The UTC date and time of the last update.</value>
    /// <example>2024-01-15T14:22:30Z</example>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets validation rules for this setting.
    /// Provides client-side validation information (min/max values, patterns, etc.).
    /// </summary>
    /// <value>A collection of validation rules applicable to this setting.</value>
    public IReadOnlyCollection<ValidationRule> ValidationRules { get; set; } = Array.Empty<ValidationRule>();

    /// <summary>
    /// Gets or sets possible predefined values for this setting.
    /// Used for enum-like settings or settings with a limited set of valid options.
    /// </summary>
    /// <value>A collection of valid options for this setting, or null if open-ended.</value>
    public IReadOnlyCollection<ConfigurationOption>? Options { get; set; }

    /// <summary>
    /// Gets whether this setting has been modified from its default value.
    /// Helps users identify customized settings.
    /// </summary>
    /// <value>True if the current value differs from the default value.</value>
    [JsonInclude]
    public bool IsModified => DefaultValue != null && !Equals(Value, DefaultValue);

    /// <summary>
    /// Gets the user-friendly data type description based on the DataType enum.
    /// </summary>
    /// <param name="dataType">The ConfigurationDataType enum value.</param>
    /// <returns>A human-readable description of the data type.</returns>
    public static string GetDataTypeDescription(ConfigurationDataType dataType)
    {
        return dataType switch
        {
            ConfigurationDataType.String => "Text",
            ConfigurationDataType.Integer => "Integer Number",
            ConfigurationDataType.Boolean => "True/False",
            ConfigurationDataType.Path => "File Path",
            ConfigurationDataType.Json => "JSON Object",
            _ => "Unknown"
        };
    }
}

/// <summary>
/// Represents a validation rule for configuration settings.
/// Provides client-side validation information in a structured format.
/// </summary>
public class ValidationRule
{
    /// <summary>
    /// Gets or sets the type of validation rule.
    /// </summary>
    /// <value>The validation rule type (e.g., "range", "pattern", "required").</value>
    /// <example>range</example>
    public required string Type { get; set; }

    /// <summary>
    /// Gets or sets the validation rule parameters.
    /// Contains rule-specific parameters like min/max values, patterns, etc.
    /// </summary>
    /// <value>A dictionary of rule parameters.</value>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Gets or sets the error message to display when validation fails.
    /// </summary>
    /// <value>A user-friendly error message for validation failures.</value>
    /// <example>Value must be between 0 and 1</example>
    public required string ErrorMessage { get; set; }
}

/// <summary>
/// Represents a predefined option for configuration settings.
/// Used for settings with a limited set of valid values.
/// </summary>
public class ConfigurationOption
{
    /// <summary>
    /// Gets or sets the option value.
    /// </summary>
    /// <value>The actual value for this option.</value>
    /// <example>high</example>
    public required object Value { get; set; }

    /// <summary>
    /// Gets or sets the display label for this option.
    /// </summary>
    /// <value>A user-friendly label for display purposes.</value>
    /// <example>High Confidence</example>
    public required string Label { get; set; }

    /// <summary>
    /// Gets or sets an optional description for this option.
    /// </summary>
    /// <value>Additional context about this option.</value>
    /// <example>Use high confidence threshold for more accurate results</example>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether this option is deprecated.
    /// </summary>
    /// <value>True if this option is deprecated and should not be used for new configurations.</value>
    /// <example>false</example>
    public bool IsDeprecated { get; set; }
}

/// <summary>
/// Groups configuration settings by section for organized display.
/// Provides hierarchical organization of configuration data.
/// </summary>
public class ConfigurationSection
{
    /// <summary>
    /// Gets or sets the section name.
    /// </summary>
    /// <value>The unique section identifier.</value>
    /// <example>ML</example>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the display title for this section.
    /// </summary>
    /// <value>A user-friendly title for the section.</value>
    /// <example>Machine Learning</example>
    public required string Title { get; set; }

    /// <summary>
    /// Gets or sets a description of this configuration section.
    /// </summary>
    /// <value>A description explaining the purpose of settings in this section.</value>
    /// <example>Settings related to machine learning classification behavior</example>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the settings in this section.
    /// </summary>
    /// <value>A collection of configuration settings belonging to this section.</value>
    public IReadOnlyCollection<ConfigurationResponse> Settings { get; set; } = Array.Empty<ConfigurationResponse>();

    /// <summary>
    /// Gets or sets the display order for this section.
    /// </summary>
    /// <value>An integer indicating the preferred display order (lower numbers first).</value>
    /// <example>1</example>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets whether this section can be collapsed in the UI.
    /// </summary>
    /// <value>True if the section supports collapse/expand functionality.</value>
    /// <example>true</example>
    public bool IsCollapsible { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this section is initially expanded.
    /// </summary>
    /// <value>True if the section should be expanded by default.</value>
    /// <example>true</example>
    public bool IsInitiallyExpanded { get; set; } = true;

    /// <summary>
    /// Gets the number of settings in this section.
    /// </summary>
    /// <value>The count of configuration settings in this section.</value>
    [JsonInclude]
    public int SettingCount => Settings?.Count ?? 0;

    /// <summary>
    /// Gets the number of modified settings in this section.
    /// </summary>
    /// <value>The count of settings that have been changed from their defaults.</value>
    [JsonInclude]
    public int ModifiedSettingCount => Settings?.Count(s => s.IsModified) ?? 0;
}

/// <summary>
/// Represents the complete configuration state for the application.
/// Provides a comprehensive view of all configuration sections and their settings.
/// </summary>
public class ConfigurationSummary
{
    /// <summary>
    /// Gets or sets all configuration sections with their settings.
    /// </summary>
    /// <value>A collection of configuration sections.</value>
    public IReadOnlyCollection<ConfigurationSection> Sections { get; set; } = Array.Empty<ConfigurationSection>();

    /// <summary>
    /// Gets or sets when this configuration summary was generated.
    /// </summary>
    /// <value>The UTC timestamp when this summary was created.</value>
    /// <example>2024-01-15T15:30:00Z</example>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the total number of configuration settings.
    /// </summary>
    /// <value>The total count of all configuration settings across all sections.</value>
    [JsonInclude]
    public int TotalSettings => Sections?.Sum(s => s.SettingCount) ?? 0;

    /// <summary>
    /// Gets or sets the number of settings requiring restart.
    /// </summary>
    /// <value>The count of settings that require application restart when changed.</value>
    [JsonInclude]
    public int RestartRequiredSettings => Sections?
        .SelectMany(s => s.Settings)
        .Count(s => s.RequiresRestart) ?? 0;

    /// <summary>
    /// Gets or sets the number of modified settings.
    /// </summary>
    /// <value>The total count of settings that have been changed from defaults.</value>
    [JsonInclude]
    public int ModifiedSettings => Sections?.Sum(s => s.ModifiedSettingCount) ?? 0;
}