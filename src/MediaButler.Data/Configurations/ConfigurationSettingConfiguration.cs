using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;

namespace MediaButler.Data.Configurations;

/// <summary>
/// Entity configuration for the ConfigurationSetting entity.
/// Defines database schema, constraints, and indexes for dynamic application configuration.
/// </summary>
/// <remarks>
/// This configuration optimizes the ConfigurationSetting table for configuration management:
/// - Fast lookup by configuration key (primary key)
/// - Section-based organization and queries
/// - Type-safe value storage and validation
/// - Change tracking for configuration audit
/// - Support for application restart requirements
/// </remarks>
public class ConfigurationSettingConfiguration : BaseEntityConfiguration<ConfigurationSetting>
{
    /// <summary>
    /// Configures the ConfigurationSetting entity with specific database schema and constraints.
    /// </summary>
    /// <param name="builder">The entity type builder for ConfigurationSetting.</param>
    protected override void ConfigureEntity(EntityTypeBuilder<ConfigurationSetting> builder)
    {
        // Table configuration
        builder.ToTable("ConfigurationSettings", schema: null);

        // Primary key configuration - Key as primary key
        builder.HasKey(c => c.Key);
        
        builder.Property(c => c.Key)
            .HasColumnName("Key")
            .HasColumnType("varchar(200)")
            .HasMaxLength(200)
            .IsRequired()
            .HasComment("Unique configuration key identifier (e.g., 'ML.ConfidenceThreshold')");

        // Configuration value stored as JSON
        builder.Property(c => c.Value)
            .HasColumnName("Value")
            .HasColumnType("text")
            .IsRequired()
            .HasComment("Configuration value serialized as JSON");

        // Section for logical grouping
        builder.Property(c => c.Section)
            .HasColumnName("Section")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired()
            .HasComment("Logical section for grouping related settings (e.g., 'ML', 'Paths')");

        // Human-readable description
        builder.Property(c => c.Description)
            .HasColumnName("Description")
            .HasColumnType("varchar(500)")
            .HasMaxLength(500)
            .IsRequired(false)
            .HasComment("Human-readable description of the setting's purpose");

        // Data type for validation and UI rendering
        builder.Property(c => c.DataType)
            .HasColumnName("DataType")
            .HasColumnType("integer")
            .IsRequired()
            .HasDefaultValue(ConfigurationDataType.String)
            .HasConversion<int>()
            .HasComment("Expected data type for value validation");

        // Restart requirement flag
        builder.Property(c => c.RequiresRestart)
            .HasColumnName("RequiresRestart")
            .HasColumnType("boolean")
            .IsRequired()
            .HasDefaultValue(false)
            .HasComment("Indicates if application restart is required for changes to take effect");

        // Configure indexes for efficient configuration access
        ConfigureConfigurationSettingIndexes(builder);

        // Add constraints for data integrity
        ConfigureConfigurationSettingConstraints(builder);
    }

    /// <summary>
    /// Configures indexes optimized for configuration management access patterns.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    /// <remarks>
    /// Indexes support:
    /// - Section-based configuration loading
    /// - Data type filtering for UI generation
    /// - Restart requirement queries for deployment
    /// - Active configuration monitoring
    /// </remarks>
    private static void ConfigureConfigurationSettingIndexes(EntityTypeBuilder<ConfigurationSetting> builder)
    {
        // Section-based queries for loading related configurations
        builder.HasIndex(c => new { c.Section, c.IsActive })
            .HasDatabaseName("IX_ConfigurationSettings_Section_Active")
            .HasFilter("[IsActive] = 1");

        // Data type filtering for UI generation and validation
        builder.HasIndex(c => new { c.DataType, c.Section, c.IsActive })
            .HasDatabaseName("IX_ConfigurationSettings_DataType_Section")
            .HasFilter("[IsActive] = 1");

        // Restart requirement queries for deployment planning
        builder.HasIndex(c => new { c.RequiresRestart, c.LastUpdateDate })
            .HasDatabaseName("IX_ConfigurationSettings_Restart_Changes")
            .HasFilter("[RequiresRestart] = 1 AND [IsActive] = 1");

        // Recently changed configuration monitoring
        builder.HasIndex(c => new { c.LastUpdateDate, c.Section })
            .HasDatabaseName("IX_ConfigurationSettings_Recent_Changes")
            .HasFilter("[IsActive] = 1")
            .IsDescending(true, false); // LastUpdateDate descending
    }

    /// <summary>
    /// Configures database constraints for data integrity and validation.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    /// <remarks>
    /// Constraints ensure:
    /// - Valid configuration key format
    /// - Reasonable value length limits
    /// - Section naming conventions
    /// - Data type consistency
    /// </remarks>
    private static void ConfigureConfigurationSettingConstraints(EntityTypeBuilder<ConfigurationSetting> builder)
    {
        // Key format constraint - should contain at least one dot for section.key format
        builder.HasCheckConstraint(
            "CK_ConfigurationSettings_Key_Format",
            "[Key] LIKE '%.%'");

        // Value length constraint - prevent extremely large configuration values
        builder.HasCheckConstraint(
            "CK_ConfigurationSettings_Value_Length",
            "LENGTH([Value]) <= 10000");

        // Section naming constraint - alphanumeric with underscores/hyphens
        builder.HasCheckConstraint(
            "CK_ConfigurationSettings_Section_Format",
            "[Section] NOT LIKE '%[^A-Za-z0-9_-]%'");

        // Data type validation - ensure valid enum values
        builder.HasCheckConstraint(
            "CK_ConfigurationSettings_DataType_Valid",
            "[DataType] BETWEEN 0 AND 4");
    }
}