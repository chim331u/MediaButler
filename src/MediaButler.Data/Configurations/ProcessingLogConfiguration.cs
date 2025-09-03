using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;

namespace MediaButler.Data.Configurations;

/// <summary>
/// Entity configuration for the ProcessingLog entity.
/// Defines database schema, relationships, and indexes for comprehensive audit logging.
/// </summary>
/// <remarks>
/// This configuration optimizes the ProcessingLog table for audit trail and debugging scenarios:
/// - Fast lookup by associated file hash
/// - Efficient filtering by log level and category
/// - Time-based queries for troubleshooting
/// - Performance monitoring through duration tracking
/// - Relationship with TrackedFile for complete audit trail
/// </remarks>
public class ProcessingLogConfiguration : BaseEntityConfiguration<ProcessingLog>
{
    /// <summary>
    /// Configures the ProcessingLog entity with specific database schema and relationships.
    /// </summary>
    /// <param name="builder">The entity type builder for ProcessingLog.</param>
    protected override void ConfigureEntity(EntityTypeBuilder<ProcessingLog> builder)
    {
        // Table configuration
        builder.ToTable("ProcessingLogs", schema: null);

        // Primary key configuration
        builder.HasKey(p => p.Id);
        
        builder.Property(p => p.Id)
            .HasColumnName("Id")
            .HasColumnType("char(36)")
            .IsRequired()
            .HasComment("Unique identifier for this log entry");

        // Foreign key relationship to TrackedFile
        builder.Property(p => p.FileHash)
            .HasColumnName("FileHash")
            .HasColumnType("varchar(64)")
            .HasMaxLength(64)
            .IsRequired()
            .HasComment("SHA256 hash of the associated file");

        // Log level configuration
        builder.Property(p => p.Level)
            .HasColumnName("Level")
            .HasColumnType("integer")
            .IsRequired()
            .HasConversion<int>()
            .HasComment("Severity level of this log entry");

        // Category for organizing log entries by functional area
        builder.Property(p => p.Category)
            .HasColumnName("Category")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired()
            .HasComment("Functional category that generated this log entry");

        // Primary log message
        builder.Property(p => p.Message)
            .HasColumnName("Message")
            .HasColumnType("varchar(1000)")
            .HasMaxLength(1000)
            .IsRequired()
            .HasComment("Primary log message describing the event");

        // Detailed information (optional)
        builder.Property(p => p.Details)
            .HasColumnName("Details")
            .HasColumnType("text")
            .IsRequired(false)
            .HasComment("Additional detailed information about the logged event");

        // Exception information for error scenarios
        builder.Property(p => p.Exception)
            .HasColumnName("Exception")
            .HasColumnType("text")
            .IsRequired(false)
            .HasComment("Exception information including stack trace for debugging");

        // Performance tracking
        builder.Property(p => p.DurationMs)
            .HasColumnName("DurationMs")
            .HasColumnType("bigint")
            .IsRequired(false)
            .HasComment("Operation duration in milliseconds for performance monitoring");

        // Configure relationships and indexes
        ConfigureProcessingLogRelationships(builder);
        ConfigureProcessingLogIndexes(builder);
    }

    /// <summary>
    /// Configures relationships between ProcessingLog and other entities.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    /// <remarks>
    /// Establishes foreign key relationship with TrackedFile to maintain referential integrity
    /// while allowing for orphaned logs if files are hard-deleted (for audit compliance).
    /// </remarks>
    private static void ConfigureProcessingLogRelationships(EntityTypeBuilder<ProcessingLog> builder)
    {
        // Foreign key relationship to TrackedFile
        // Note: Using DeleteBehavior.Restrict to preserve audit trail even if file is hard-deleted
        builder.HasOne<TrackedFile>()
            .WithMany()
            .HasForeignKey(p => p.FileHash)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ProcessingLogs_TrackedFiles");

        // Add index on foreign key for relationship queries
        builder.HasIndex(p => p.FileHash)
            .HasDatabaseName("IX_ProcessingLogs_FileHash");
    }

    /// <summary>
    /// Configures indexes optimized for log querying and analysis patterns.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    /// <remarks>
    /// Indexes support common logging scenarios:
    /// - Error monitoring and alerting
    /// - Performance analysis and optimization
    /// - Category-based log filtering
    /// - Time-based troubleshooting queries
    /// - Audit trail reconstruction
    /// </remarks>
    private static void ConfigureProcessingLogIndexes(EntityTypeBuilder<ProcessingLog> builder)
    {
        // Error monitoring index - Level + CreatedDate for alert systems
        builder.HasIndex(p => new { p.Level, p.CreatedDate, p.IsActive })
            .HasDatabaseName("IX_ProcessingLogs_Error_Monitoring")
            .HasFilter("[Level] >= 4 AND [IsActive] = 1"); // Error and Critical levels

        // Category-based filtering with time ordering
        builder.HasIndex(p => new { p.Category, p.CreatedDate, p.IsActive })
            .HasDatabaseName("IX_ProcessingLogs_Category_Timeline")
            .HasFilter("[IsActive] = 1");

        // Performance analysis index
        builder.HasIndex(p => new { p.Category, p.DurationMs, p.CreatedDate })
            .HasDatabaseName("IX_ProcessingLogs_Performance_Analysis")
            .HasFilter("[DurationMs] IS NOT NULL AND [IsActive] = 1");

        // File-specific audit trail with chronological ordering
        builder.HasIndex(p => new { p.FileHash, p.CreatedDate, p.Level })
            .HasDatabaseName("IX_ProcessingLogs_File_Audit_Trail")
            .HasFilter("[IsActive] = 1");

        // Recent activity monitoring - last 24/48 hours queries
        builder.HasIndex(p => new { p.CreatedDate, p.Level, p.Category })
            .HasDatabaseName("IX_ProcessingLogs_Recent_Activity")
            .HasFilter("[IsActive] = 1")
            .IsDescending(true, false, false); // CreatedDate descending for recent-first

        // Exception tracking for debugging
        builder.HasIndex(p => new { p.Exception, p.CreatedDate })
            .HasDatabaseName("IX_ProcessingLogs_Exception_Tracking")
            .HasFilter("[Exception] IS NOT NULL AND [IsActive] = 1");

        // Composite index for comprehensive log analysis
        builder.HasIndex(p => new { p.Level, p.Category, p.FileHash, p.CreatedDate })
            .HasDatabaseName("IX_ProcessingLogs_Comprehensive_Analysis")
            .HasFilter("[IsActive] = 1");
    }
}