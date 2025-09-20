namespace MediaButler.Web.Models;

/// <summary>
/// Configuration response DTO matching MediaButler.API structure.
/// Following "Simple Made Easy" principles with immutable data representation.
/// </summary>
public record ConfigurationDto(
    string Key,
    object? Value,
    string RawValue,
    string Section,
    string? Description,
    ConfigurationDataType DataType,
    string DataTypeDescription,
    bool RequiresRestart,
    bool IsEditable,
    object? DefaultValue,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyCollection<ValidationRuleDto> ValidationRules,
    IReadOnlyCollection<ConfigurationOptionDto>? Options
)
{
    /// <summary>
    /// Gets whether this setting has been modified from its default value.
    /// </summary>
    public bool IsModified => DefaultValue != null && !Equals(Value, DefaultValue);
}

/// <summary>
/// Configuration data types enum matching API.
/// </summary>
public enum ConfigurationDataType
{
    String,
    Integer,
    Boolean,
    Path,
    Json
}

/// <summary>
/// Validation rule DTO for client-side validation.
/// </summary>
public record ValidationRuleDto(
    string Type,
    Dictionary<string, object> Parameters,
    string ErrorMessage
);

/// <summary>
/// Configuration option DTO for predefined values.
/// </summary>
public record ConfigurationOptionDto(
    object Value,
    string Label,
    string? Description,
    bool IsDeprecated
);

/// <summary>
/// Request DTO for updating configuration settings.
/// </summary>
public record UpdateConfigurationRequest(
    object Value,
    string? Section = null,
    string? Description = null,
    bool RequiresRestart = false
);

/// <summary>
/// Request DTO for creating new configuration settings.
/// </summary>
public record CreateConfigurationRequest(
    string Key,
    object Value,
    string Section,
    string? Description = null,
    bool RequiresRestart = false
);

/// <summary>
/// View model for configuration management UI.
/// Separates API concerns from UI presentation.
/// </summary>
public record ConfigurationViewModel(
    string Key,
    object? Value,
    string RawValue,
    string Section,
    string? Description,
    string DataTypeDescription,
    bool RequiresRestart,
    bool IsEditable,
    object? DefaultValue,
    bool IsModified,
    DateTime UpdatedAt
)
{
    /// <summary>
    /// Gets a display-friendly value representation.
    /// </summary>
    public string DisplayValue => Value?.ToString() ?? "null";

    /// <summary>
    /// Gets a short description for grid display.
    /// </summary>
    public string ShortDescription => Description?.Length > 50
        ? Description[..47] + "..."
        : Description ?? "";
}

/// <summary>
/// Search filter for configuration settings.
/// </summary>
public record ConfigurationFilter(
    string? SearchPattern = null,
    string? Section = null,
    bool? RequiresRestart = null,
    bool? IsModified = null
);

/// <summary>
/// Result wrapper for configuration operations.
/// </summary>
public record ConfigurationOperationResult(
    bool IsSuccess,
    string? ErrorMessage = null,
    ConfigurationDto? UpdatedSetting = null
);

/// <summary>
/// Response DTO for watch folders reload operation.
/// </summary>
public record ReloadWatchFoldersResponse(
    string Message,
    DateTime ReloadedAt,
    IEnumerable<string> MonitoredPaths
);