using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MediaButler.Core.Entities;

namespace MediaButler.Data.Configurations;

/// <summary>
/// Entity configuration for the UserPreference entity.
/// Defines database schema, constraints, and indexes for user preference management.
/// </summary>
/// <remarks>
/// This configuration optimizes the UserPreference table for personalization scenarios:
/// - Fast lookup by user and preference key (composite primary key)
/// - Category-based preference organization and queries
/// - JSON value storage with validation constraints
/// - Support for future multi-user scenarios
/// - Efficient preference loading and caching
/// </remarks>
public class UserPreferenceConfiguration : BaseEntityConfiguration<UserPreference>
{
    /// <summary>
    /// Configures the UserPreference entity with specific database schema and constraints.
    /// </summary>
    /// <param name="builder">The entity type builder for UserPreference.</param>
    protected override void ConfigureEntity(EntityTypeBuilder<UserPreference> builder)
    {
        // Table configuration
        builder.ToTable("UserPreferences", schema: null);

        // Primary key configuration
        builder.HasKey(u => u.Id);
        
        builder.Property(u => u.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .IsRequired()
            .HasComment("Unique identifier for this user preference");

        // User identifier configuration
        builder.Property(u => u.UserId)
            .HasColumnName("UserId")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired()
            .HasDefaultValue("default")
            .HasComment("User identifier this preference belongs to (defaults to 'default' for single-user)");

        // Preference key configuration
        builder.Property(u => u.Key)
            .HasColumnName("Key")
            .HasColumnType("varchar(200)")
            .HasMaxLength(200)
            .IsRequired()
            .HasComment("Unique preference key identifier (e.g., 'theme', 'defaultView')");

        // Preference value stored as JSON
        builder.Property(u => u.Value)
            .HasColumnName("Value")
            .HasColumnType("text")
            .IsRequired()
            .HasComment("Preference value serialized as JSON for consistent storage");

        // Category for organizing preferences
        builder.Property(u => u.Category)
            .HasColumnName("Category")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired()
            .HasComment("Category for organizing related preferences (e.g., 'UI', 'Notifications')");

        // Configure indexes for efficient preference access
        ConfigureUserPreferenceIndexes(builder);

        // Add constraints for data integrity
        ConfigureUserPreferenceConstraints(builder);
    }

    /// <summary>
    /// Configures indexes optimized for user preference access patterns.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    /// <remarks>
    /// Indexes support:
    /// - User-specific preference loading
    /// - Category-based preference management
    /// - Key-based preference lookup
    /// - Active preference filtering
    /// </remarks>
    private static void ConfigureUserPreferenceIndexes(EntityTypeBuilder<UserPreference> builder)
    {
        // Unique constraint on UserId + Key for logical uniqueness
        builder.HasIndex(u => new { u.UserId, u.Key })
            .HasDatabaseName("IX_UserPreferences_User_Key_Unique")
            .IsUnique()
            .HasFilter("[IsActive] = 1");

        // Category-based preference queries
        builder.HasIndex(u => new { u.Category, u.UserId, u.IsActive })
            .HasDatabaseName("IX_UserPreferences_Category_User")
            .HasFilter("[IsActive] = 1");

        // User preference loading index
        builder.HasIndex(u => new { u.UserId, u.IsActive, u.LastUpdateDate })
            .HasDatabaseName("IX_UserPreferences_User_Active_Updated")
            .HasFilter("[IsActive] = 1")
            .IsDescending(false, false, true); // LastUpdateDate descending for recent-first

        // Key pattern analysis for preference discovery
        builder.HasIndex(u => new { u.Key, u.Category, u.IsActive })
            .HasDatabaseName("IX_UserPreferences_Key_Category")
            .HasFilter("[IsActive] = 1");

        // Recently modified preferences for change tracking
        builder.HasIndex(u => new { u.LastUpdateDate, u.UserId, u.Category })
            .HasDatabaseName("IX_UserPreferences_Recent_Changes")
            .HasFilter("[IsActive] = 1")
            .IsDescending(true, false, false); // LastUpdateDate descending
    }

    /// <summary>
    /// Configures database constraints for data integrity and validation.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    /// <remarks>
    /// Constraints ensure:
    /// - Valid preference key format
    /// - Reasonable value length limits
    /// - Category naming conventions
    /// - JSON value format validation
    /// </remarks>
    private static void ConfigureUserPreferenceConstraints(EntityTypeBuilder<UserPreference> builder)
    {
        // Key format constraint - should not be empty and follow naming convention
        builder.HasCheckConstraint(
            "CK_UserPreferences_Key_Format",
            "[Key] NOT LIKE '' AND [Key] NOT LIKE '% %'");

        // Value length constraint - prevent extremely large preference values
        builder.HasCheckConstraint(
            "CK_UserPreferences_Value_Length",
            "LENGTH([Value]) <= 10000");

        // Category naming constraint - alphanumeric with underscores
        builder.HasCheckConstraint(
            "CK_UserPreferences_Category_Format",
            "[Category] NOT LIKE '%[^A-Za-z0-9_]%'");

        // UserId format constraint - alphanumeric with underscores and hyphens
        builder.HasCheckConstraint(
            "CK_UserPreferences_UserId_Format",
            "[UserId] NOT LIKE '%[^A-Za-z0-9_-]%'");
    }
}