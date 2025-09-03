using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MediaButler.Core.Common;

namespace MediaButler.Data.Configurations;

/// <summary>
/// Abstract base configuration for all entities that inherit from BaseEntity.
/// This configuration provides consistent setup for audit properties and common indexes
/// following "Simple Made Easy" principles with shared, composable configuration logic.
/// </summary>
/// <typeparam name="TEntity">The entity type that inherits from BaseEntity.</typeparam>
/// <remarks>
/// This base configuration ensures all entities have consistent:
/// - Column configurations for audit properties
/// - Indexes for common query patterns
/// - Soft delete support
/// - Standard naming conventions
/// 
/// Derived configurations should focus on entity-specific concerns without
/// duplicating the common BaseEntity setup.
/// </remarks>
public abstract class BaseEntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : BaseEntity
{
    /// <summary>
    /// Configures the entity with BaseEntity properties and common patterns.
    /// Derived classes should override ConfigureEntity for specific configuration.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the entity type.</param>
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        // Configure BaseEntity audit properties with appropriate column settings
        ConfigureAuditProperties(builder);

        // Setup common indexes for performance optimization
        ConfigureCommonIndexes(builder);

        // Allow derived classes to add specific configuration
        ConfigureEntity(builder);
    }

    /// <summary>
    /// Configures entity-specific properties and relationships.
    /// Derived classes should override this method to provide entity-specific configuration.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the entity type.</param>
    protected abstract void ConfigureEntity(EntityTypeBuilder<TEntity> builder);

    /// <summary>
    /// Configures the audit properties inherited from BaseEntity.
    /// Sets up column types, constraints, and default values for consistent audit trail.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    /// <remarks>
    /// This method ensures all audit properties have:
    /// - Proper column types for efficient storage
    /// - NOT NULL constraints where appropriate
    /// - Default values for IsActive (true for new entities)
    /// - Appropriate column names following conventions
    /// </remarks>
    private static void ConfigureAuditProperties(EntityTypeBuilder<TEntity> builder)
    {
        // CreatedDate configuration
        builder.Property(e => e.CreatedDate)
            .HasColumnName("CreatedDate")
            .HasColumnType("datetime")
            .IsRequired()
            .HasComment("UTC timestamp when the entity was created");

        // LastUpdateDate configuration
        builder.Property(e => e.LastUpdateDate)
            .HasColumnName("LastUpdateDate")
            .HasColumnType("datetime")
            .IsRequired()
            .HasComment("UTC timestamp when the entity was last modified");

        // Note configuration - optional text field
        builder.Property(e => e.Note)
            .HasColumnName("Note")
            .HasColumnType("text")
            .IsRequired(false)
            .HasComment("Optional contextual notes about the entity");

        // IsActive configuration - soft delete flag
        builder.Property(e => e.IsActive)
            .HasColumnName("IsActive")
            .HasColumnType("boolean")
            .IsRequired()
            .HasDefaultValue(true)
            .HasComment("Indicates if the entity is active (not soft-deleted)");
    }

    /// <summary>
    /// Configures common indexes that are useful across all entities.
    /// These indexes optimize common query patterns for audit and soft delete scenarios.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    /// <remarks>
    /// Standard indexes created:
    /// - IsActive index for soft delete queries
    /// - CreatedDate index for temporal queries
    /// - LastUpdateDate index for recent changes queries
    /// - Composite index on IsActive + LastUpdateDate for common patterns
    /// </remarks>
    private static void ConfigureCommonIndexes(EntityTypeBuilder<TEntity> builder)
    {
        // Index on IsActive for efficient soft delete filtering
        builder.HasIndex(e => e.IsActive)
            .HasDatabaseName($"IX_{typeof(TEntity).Name}_IsActive");

        // Index on CreatedDate for temporal queries
        builder.HasIndex(e => e.CreatedDate)
            .HasDatabaseName($"IX_{typeof(TEntity).Name}_CreatedDate");

        // Index on LastUpdateDate for recent changes queries
        builder.HasIndex(e => e.LastUpdateDate)
            .HasDatabaseName($"IX_{typeof(TEntity).Name}_LastUpdateDate");

        // Composite index for common active entity queries ordered by last update
        builder.HasIndex(e => new { e.IsActive, e.LastUpdateDate })
            .HasDatabaseName($"IX_{typeof(TEntity).Name}_IsActive_LastUpdateDate");
    }
}