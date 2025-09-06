using MediaButler.Core.Entities;
using MediaButler.Tests.Unit.Builders;

namespace MediaButler.Tests.Unit.ObjectMothers;

/// <summary>
/// Object Mother for creating common ConfigurationSetting test scenarios.
/// Provides pre-configured settings for typical system configuration cases.
/// </summary>
public static class ConfigurationObjectMother
{
    /// <summary>
    /// Creates default path configuration settings.
    /// Scenario: Standard MediaButler path configuration.
    /// </summary>
    public static IEnumerable<ConfigurationSetting> DefaultPathSettings()
    {
        yield return new ConfigurationSettingBuilder()
            .AsPath("WatchFolder", "/mnt/nas/downloads/completed")
            .Build();
            
        yield return new ConfigurationSettingBuilder()
            .AsPath("MediaLibrary", "/mnt/nas/TV Series")
            .Build();
            
        yield return new ConfigurationSettingBuilder()
            .AsPath("PendingReview", "/mnt/nas/MediaButler/Pending")
            .Build();
            
        yield return new ConfigurationSettingBuilder()
            .AsPath("ModelsPath", "./models")
            .Build();
    }

    /// <summary>
    /// Creates default ML configuration settings.
    /// Scenario: Standard machine learning configuration.
    /// </summary>
    public static IEnumerable<ConfigurationSetting> DefaultMLSettings()
    {
        yield return new ConfigurationSettingBuilder()
            .AsMLSetting("ModelType", "FastText")
            .Build();
            
        yield return new ConfigurationSettingBuilder()
            .AsMLSetting("ConfidenceThreshold", "0.75")
            .Build();
            
        yield return new ConfigurationSettingBuilder()
            .AsMLSetting("MaxConcurrentClassifications", "2")
            .Build();
            
        yield return new ConfigurationSettingBuilder()
            .AsMLSetting("RetrainInterval", "Weekly")
            .Build();
    }

    /// <summary>
    /// Creates default butler behavior settings.
    /// Scenario: Standard processing behavior configuration.
    /// </summary>
    public static IEnumerable<ConfigurationSetting> DefaultButlerSettings()
    {
        yield return new ConfigurationSettingBuilder()
            .AsButlerSetting("ScanInterval", "60")
            .Build();
            
        yield return new ConfigurationSettingBuilder()
            .AsButlerSetting("MaxRetries", "3")
            .Build();
            
        yield return new ConfigurationSettingBuilder()
            .AsButlerSetting("AutoOrganize", "false")
            .Build();
    }

    /// <summary>
    /// Creates ARM32-optimized configuration settings.
    /// Scenario: Performance-constrained deployment settings.
    /// </summary>
    public static IEnumerable<ConfigurationSetting> ARM32OptimizedSettings()
    {
        yield return new ConfigurationSettingBuilder()
            .AsButlerSetting("MaxConcurrentOperations", "2")
            .WithDescription("Reduced concurrency for ARM32 memory constraints")
            .Build();
            
        yield return new ConfigurationSettingBuilder()
            .AsMLSetting("BatchSize", "10")
            .WithDescription("Smaller batch sizes for ARM32 processing")
            .Build();
            
        yield return new ConfigurationSettingBuilder()
            .WithKey("MediaButler.Performance.MemoryThresholdMB")
            .WithValue("250")
            .WithDescription("Memory threshold before garbage collection")
            .RequiresRestart(true)
            .Build();
    }

    /// <summary>
    /// Creates test-specific configuration settings.
    /// Scenario: Configuration for testing environments.
    /// </summary>
    public static IEnumerable<ConfigurationSetting> TestSettings()
    {
        yield return new ConfigurationSettingBuilder()
            .AsPath("WatchFolder", "/tmp/test/downloads")
            .Build();
            
        yield return new ConfigurationSettingBuilder()
            .AsPath("MediaLibrary", "/tmp/test/library")
            .Build();
            
        yield return new ConfigurationSettingBuilder()
            .AsMLSetting("ConfidenceThreshold", "0.5")
            .WithDescription("Lower threshold for testing")
            .Build();
    }

    /// <summary>
    /// Creates a single path setting for simple tests.
    /// Scenario: Testing individual configuration operations.
    /// </summary>
    public static ConfigurationSetting WatchFolderSetting(string path = "/downloads")
    {
        return new ConfigurationSettingBuilder()
            .AsPath("WatchFolder", path)
            .Build();
    }

    /// <summary>
    /// Creates a single ML setting for simple tests.
    /// Scenario: Testing ML configuration changes.
    /// </summary>
    public static ConfigurationSetting ConfidenceThresholdSetting(double threshold = 0.75)
    {
        return new ConfigurationSettingBuilder()
            .AsMLSetting("ConfidenceThreshold", threshold.ToString())
            .Build();
    }

    /// <summary>
    /// Creates a setting that requires restart.
    /// Scenario: Testing restart requirement handling.
    /// </summary>
    public static ConfigurationSetting RestartRequiredSetting()
    {
        return new ConfigurationSettingBuilder()
            .WithKey("MediaButler.Database.ConnectionString")
            .WithValue("Data Source=test.db")
            .WithDescription("Database connection string")
            .RequiresRestart(true)
            .Build();
    }

    /// <summary>
    /// Creates all default system configuration.
    /// Scenario: Full system configuration for comprehensive tests.
    /// </summary>
    public static IEnumerable<ConfigurationSetting> AllDefaultSettings()
    {
        return DefaultPathSettings()
            .Concat(DefaultMLSettings())
            .Concat(DefaultButlerSettings());
    }
}