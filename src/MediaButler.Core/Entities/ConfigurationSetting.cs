using System;
using MediaButler.Core.Common;
using MediaButler.Core.Enums;

namespace MediaButler.Core.Entities;

/// <summary>
/// Represents a dynamic configuration setting in the MediaButler system.
/// This entity enables runtime configuration changes without application restarts,
/// following "Simple Made Easy" principles by separating configuration concerns
/// from business logic and maintaining a single responsibility for setting storage.
/// </summary>
/// <remarks>
/// ConfigurationSetting provides type-safe configuration management with validation
/// and metadata support. Settings are organized by section and support various
/// data types with automatic serialization to JSON for storage.
/// </remarks>
public class ConfigurationSetting : BaseEntity
{
    /// <summary>
    /// Gets or sets the unique key identifier for this configuration setting.
    /// Serves as the primary key and must be unique across all configuration settings.
    /// </summary>
    /// <value>A unique string key identifying this setting (e.g., "ML.ConfidenceThreshold").</value>
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets the configuration value stored as a JSON string.
    /// All values are serialized to JSON to provide consistent storage format
    /// regardless of the underlying data type.
    /// </summary>
    /// <value>The setting value serialized as JSON.</value>
    public required string Value { get; set; }

    /// <summary>
    /// Gets or sets the logical section this setting belongs to.
    /// Used for organizing related settings and implementing section-based access.
    /// </summary>
    /// <value>The section name (e.g., "ML", "Paths", "Butler").</value>
    public required string Section { get; set; }

    /// <summary>
    /// Gets or sets a human-readable description of this setting.
    /// Provides context for administrators and users about the setting's purpose.
    /// </summary>
    /// <value>A descriptive explanation of what this setting controls.</value>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the data type of this setting's value.
    /// Used for validation and proper type conversion when retrieving values.
    /// </summary>
    /// <value>A ConfigurationDataType enum indicating the expected value type.</value>
    public ConfigurationDataType DataType { get; set; } = ConfigurationDataType.String;

    /// <summary>
    /// Gets or sets a value indicating whether changing this setting requires an application restart.
    /// Some settings may only be read during application startup and require restart to take effect.
    /// </summary>
    /// <value>true if the application must restart for changes to take effect; otherwise, false.</value>
    public bool RequiresRestart { get; set; } = false;

    /// <summary>
    /// Updates the value of this configuration setting with proper validation and audit trail.
    /// Validates the new value against the configured data type and updates audit information.
    /// </summary>
    /// <param name="newValue">The new value to set, which will be validated against DataType.</param>
    /// <param name="reason">Optional reason for the change to be recorded in audit trail.</param>
    /// <exception cref="ArgumentException">Thrown when the new value doesn't match the configured DataType.</exception>
    /// <exception cref="ArgumentNullException">Thrown when newValue is null.</exception>
    public void UpdateValue(string newValue, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(newValue))
            throw new ArgumentNullException(nameof(newValue));

        // Validate the new value against the configured data type
        ValidateValueForDataType(newValue, DataType);

        Value = newValue;
        MarkAsModified();

        // Record the reason for the change in the audit trail
        if (!string.IsNullOrWhiteSpace(reason))
        {
            Note = string.IsNullOrWhiteSpace(Note) 
                ? $"Updated: {reason}" 
                : $"{Note}\nUpdated: {reason}";
        }
    }

    /// <summary>
    /// Validates that a value string is compatible with the specified data type.
    /// Ensures type safety when updating configuration values.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="dataType">The expected data type.</param>
    /// <exception cref="ArgumentException">Thrown when the value is incompatible with the data type.</exception>
    private static void ValidateValueForDataType(string value, ConfigurationDataType dataType)
    {
        try
        {
            switch (dataType)
            {
                case ConfigurationDataType.Integer:
                    if (!int.TryParse(value, out _))
                        throw new ArgumentException($"Value '{value}' is not a valid integer.");
                    break;

                case ConfigurationDataType.Boolean:
                    if (!bool.TryParse(value, out _))
                        throw new ArgumentException($"Value '{value}' is not a valid boolean.");
                    break;

                case ConfigurationDataType.Path:
                    // Basic path validation - check for invalid characters
                    var invalidChars = System.IO.Path.GetInvalidPathChars();
                    if (value.IndexOfAny(invalidChars) >= 0)
                        throw new ArgumentException($"Value '{value}' contains invalid path characters.");
                    break;

                case ConfigurationDataType.Json:
                    // Basic JSON validation - attempt to parse
                    System.Text.Json.JsonDocument.Parse(value);
                    break;

                case ConfigurationDataType.String:
                    // String values are always valid
                    break;

                default:
                    throw new ArgumentException($"Unknown data type: {dataType}");
            }
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new ArgumentException($"Value '{value}' is not valid JSON: {ex.Message}");
        }
    }
}