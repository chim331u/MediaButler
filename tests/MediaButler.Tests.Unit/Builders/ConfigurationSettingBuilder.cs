using MediaButler.Core.Entities;

namespace MediaButler.Tests.Unit.Builders;

/// <summary>
/// Test data builder for ConfigurationSetting entities.
/// Provides fluent interface for creating test configuration data.
/// Follows "Simple Made Easy" by avoiding complex setup scenarios.
/// </summary>
public class ConfigurationSettingBuilder
{
    private string _key = "MediaButler.Test.Setting";
    private string _value = "test_value";
    private string? _description = "Test configuration setting";
    private bool _requiresRestart = false;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime _updatedAt = DateTime.UtcNow;

    /// <summary>
    /// Sets the configuration key.
    /// </summary>
    public ConfigurationSettingBuilder WithKey(string key)
    {
        _key = key;
        return this;
    }

    /// <summary>
    /// Sets the configuration value.
    /// </summary>
    public ConfigurationSettingBuilder WithValue(string value)
    {
        _value = value;
        return this;
    }

    /// <summary>
    /// Sets the description.
    /// </summary>
    public ConfigurationSettingBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Marks the setting as requiring restart.
    /// </summary>
    public ConfigurationSettingBuilder RequiresRestart(bool requiresRestart = true)
    {
        _requiresRestart = requiresRestart;
        return this;
    }

    /// <summary>
    /// Sets the timestamps.
    /// </summary>
    public ConfigurationSettingBuilder WithTimestamps(DateTime createdAt, DateTime updatedAt)
    {
        _createdAt = createdAt;
        _updatedAt = updatedAt;
        return this;
    }

    /// <summary>
    /// Creates a path configuration setting.
    /// Common scenario: File system path settings.
    /// </summary>
    public ConfigurationSettingBuilder AsPath(string pathKey, string pathValue)
    {
        return WithKey($"MediaButler.Paths.{pathKey}")
               .WithValue(pathValue)
               .WithDescription($"File system path for {pathKey.ToLower()}")
               .RequiresRestart(true);
    }

    /// <summary>
    /// Creates an ML configuration setting.
    /// Common scenario: Machine learning parameters.
    /// </summary>
    public ConfigurationSettingBuilder AsMLSetting(string mlKey, string mlValue)
    {
        return WithKey($"MediaButler.ML.{mlKey}")
               .WithValue(mlValue)
               .WithDescription($"ML configuration for {mlKey.ToLower()}")
               .RequiresRestart(false);
    }

    /// <summary>
    /// Creates a butler behavior setting.
    /// Common scenario: Processing behavior configuration.
    /// </summary>
    public ConfigurationSettingBuilder AsButlerSetting(string behaviorKey, string behaviorValue)
    {
        return WithKey($"MediaButler.Butler.{behaviorKey}")
               .WithValue(behaviorValue)
               .WithDescription($"Butler behavior setting for {behaviorKey.ToLower()}")
               .RequiresRestart(false);
    }

    /// <summary>
    /// Builds the ConfigurationSetting instance.
    /// </summary>
    public ConfigurationSetting Build()
    {
        // Extract section from key (e.g., "MediaButler.Paths.WatchFolder" -> "MediaButler.Paths")
        var sections = _key.Split('.');
        var section = sections.Length > 1 ? string.Join(".", sections.Take(sections.Length - 1)) : "Default";
        
        var setting = new ConfigurationSetting
        {
            Key = _key,
            Section = section,
            Value = _value,
            Description = _description,
            RequiresRestart = _requiresRestart
        };
        
        // Set BaseEntity properties via reflection for test data
        var baseEntityType = typeof(ConfigurationSetting).BaseType;
        baseEntityType?.GetProperty("CreatedDate")?.SetValue(setting, _createdAt);
        baseEntityType?.GetProperty("LastUpdateDate")?.SetValue(setting, _updatedAt);
        baseEntityType?.GetProperty("IsActive")?.SetValue(setting, true);
        
        return setting;
    }
}