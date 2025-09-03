using System;
using MediaButler.Core.Common;

namespace MediaButler.Core.Entities;

/// <summary>
/// Represents a user-specific preference setting in the MediaButler system.
/// This entity provides personalization capabilities while maintaining separation
/// from system configuration, following "Simple Made Easy" principles by
/// having a single responsibility: storing user preference data.
/// </summary>
/// <remarks>
/// UserPreference supports future multi-user scenarios while currently operating
/// in single-user mode. All preferences are stored as JSON to provide flexibility
/// for various data types while maintaining consistent storage format.
/// </remarks>
public class UserPreference : BaseEntity
{
    /// <summary>
    /// Gets or sets the unique identifier for this preference entry.
    /// Serves as the primary key for the UserPreference entity.
    /// </summary>
    /// <value>A unique GUID identifying this user preference.</value>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the user identifier this preference belongs to.
    /// Currently defaults to "default" for single-user operation,
    /// but supports future multi-user scenarios.
    /// </summary>
    /// <value>The user identifier, defaulting to "default" for single-user mode.</value>
    public string UserId { get; set; } = "default";

    /// <summary>
    /// Gets or sets the preference key identifier.
    /// Must be unique within the scope of a user and category.
    /// </summary>
    /// <value>A string key identifying the preference (e.g., "theme", "defaultView").</value>
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets the preference value stored as JSON.
    /// All preference values are serialized to JSON to provide consistent
    /// storage format regardless of the underlying data type.
    /// </summary>
    /// <value>The preference value serialized as JSON.</value>
    public required string Value { get; set; }

    /// <summary>
    /// Gets or sets the category this preference belongs to.
    /// Used for organizing related preferences and implementing category-based access.
    /// </summary>
    /// <value>The preference category (e.g., "UI", "Notifications", "FileHandling").</value>
    public required string Category { get; set; }

    /// <summary>
    /// Updates the value of this user preference with proper audit trail.
    /// Validates that the new value is valid JSON and updates audit information.
    /// </summary>
    /// <param name="newValue">The new preference value, which must be valid JSON.</param>
    /// <param name="reason">Optional reason for the change to be recorded in audit trail.</param>
    /// <exception cref="ArgumentException">Thrown when the new value is not valid JSON.</exception>
    /// <exception cref="ArgumentNullException">Thrown when newValue is null.</exception>
    public void UpdateValue(string newValue, string? reason = null)
    {
        if (string.IsNullOrWhiteSpace(newValue))
            throw new ArgumentNullException(nameof(newValue));

        // Validate that the new value is valid JSON
        try
        {
            System.Text.Json.JsonDocument.Parse(newValue);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new ArgumentException($"Value must be valid JSON: {ex.Message}", nameof(newValue));
        }

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
    /// Gets the typed value of this preference by deserializing the JSON value.
    /// Provides type-safe access to preference values with automatic JSON deserialization.
    /// </summary>
    /// <typeparam name="T">The expected type of the preference value.</typeparam>
    /// <returns>The deserialized preference value of type T.</returns>
    /// <exception cref="System.Text.Json.JsonException">Thrown when the stored value cannot be deserialized to type T.</exception>
    public T? GetTypedValue<T>()
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(Value);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new System.Text.Json.JsonException($"Cannot deserialize preference value to type {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Sets the typed value of this preference by serializing the value to JSON.
    /// Provides type-safe setting of preference values with automatic JSON serialization.
    /// </summary>
    /// <typeparam name="T">The type of the value to store.</typeparam>
    /// <param name="value">The strongly-typed value to store as a preference.</param>
    /// <param name="reason">Optional reason for the change to be recorded in audit trail.</param>
    public void SetTypedValue<T>(T value, string? reason = null)
    {
        try
        {
            var jsonValue = System.Text.Json.JsonSerializer.Serialize(value);
            UpdateValue(jsonValue, reason);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new System.Text.Json.JsonException($"Cannot serialize value of type {typeof(T).Name} to JSON: {ex.Message}", ex);
        }
    }
}