using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MediaButler.Core.Enums;

namespace MediaButler.Web.Models.Configuration;

/// <summary>
/// Client-side configuration setting model for web interface.
/// Mirrors API ConfigurationResponse with additional UI properties.
/// </summary>
public class ConfigurationSettingModel
{
    public required string Key { get; set; }
    public object? Value { get; set; }
    public string RawValue { get; set; } = string.Empty;
    public required string Section { get; set; }
    public string? Description { get; set; }
    public ConfigurationDataType DataType { get; set; }
    public string DataTypeDescription { get; set; } = string.Empty;
    public bool RequiresRestart { get; set; }
    public bool IsEditable { get; set; } = true;
    public object? DefaultValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ValidationRuleModel> ValidationRules { get; set; } = new();
    public List<ConfigurationOptionModel>? Options { get; set; }
    
    // UI-specific properties
    public bool IsModified => DefaultValue != null && !Equals(Value, DefaultValue);
    public bool IsExpanded { get; set; } = false;
    public bool IsEditing { get; set; } = false;
    public string? EditingValue { get; set; }
    public string? ValidationError { get; set; }
    public bool HasUnsavedChanges { get; set; } = false;
}

/// <summary>
/// Configuration section model for organizing settings in the UI.
/// </summary>
public class ConfigurationSectionModel
{
    public required string Name { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public List<ConfigurationSettingModel> Settings { get; set; } = new();
    public int Order { get; set; }
    public bool IsCollapsible { get; set; } = true;
    public bool IsExpanded { get; set; } = true;
    public string Icon { get; set; } = "settings";
    
    public int SettingCount => Settings.Count;
    public int ModifiedSettingCount => Settings.Count(s => s.IsModified);
    public int UnsavedChangesCount => Settings.Count(s => s.HasUnsavedChanges);
}

/// <summary>
/// Validation rule model for client-side validation.
/// </summary>
public class ValidationRuleModel
{
    public required string Type { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public required string ErrorMessage { get; set; }
}

/// <summary>
/// Configuration option model for settings with predefined values.
/// </summary>
public class ConfigurationOptionModel
{
    public required object Value { get; set; }
    public required string Label { get; set; }
    public string? Description { get; set; }
    public bool IsDeprecated { get; set; }
}

/// <summary>
/// Update configuration request model.
/// </summary>
public class UpdateConfigurationRequestModel
{
    [Required(ErrorMessage = "Configuration value is required")]
    public required object Value { get; set; }

    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters")]
    public string? Description { get; set; }

    public bool RequiresRestart { get; set; }
}

/// <summary>
/// Create configuration request model.
/// </summary>
public class CreateConfigurationRequestModel
{
    [Required(ErrorMessage = "Configuration key is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Configuration key must be between 3 and 100 characters")]
    [RegularExpression(@"^[A-Za-z][A-Za-z0-9]*(\.[A-Za-z][A-Za-z0-9]*)*$", ErrorMessage = "Configuration key must be in format 'Section.Key' with alphanumeric characters")]
    public required string Key { get; set; }

    [Required(ErrorMessage = "Configuration value is required")]
    public required object Value { get; set; }

    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters")]
    public string? Description { get; set; }

    public bool RequiresRestart { get; set; }
}


/// <summary>
/// Configuration export model.
/// </summary>
public class ConfigurationExportModel
{
    public string Format { get; set; } = "json";
    public bool IncludeDefaults { get; set; } = false;
    public bool IncludeDescriptions { get; set; } = true;
    public List<string> Sections { get; set; } = new();
}

/// <summary>
/// Configuration search model.
/// </summary>
public class ConfigurationSearchModel
{
    public string SearchTerm { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public bool ModifiedOnly { get; set; } = false;
    public bool RestartRequiredOnly { get; set; } = false;
    public ConfigurationDataType? DataType { get; set; }
}