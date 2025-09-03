using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;

namespace MediaButler.Data.Configurations;

/// <summary>
/// Entity configuration for the TrackedFile entity.
/// Defines database schema, indexes, constraints, and relationships for file tracking.
/// </summary>
/// <remarks>
/// This configuration optimizes the TrackedFile table for the primary MediaButler workflows:
/// - Fast lookup by file hash (primary key)
/// - Efficient queries by processing status
/// - ML classification result storage and retrieval
/// - File organization path management
/// - Error tracking and retry logic
/// </remarks>
public class TrackedFileConfiguration : BaseEntityConfiguration<TrackedFile>
{
    /// <summary>
    /// Configures the TrackedFile entity with specific database schema and constraints.
    /// </summary>
    /// <param name="builder">The entity type builder for TrackedFile.</param>
    protected override void ConfigureEntity(EntityTypeBuilder<TrackedFile> builder)
    {
        // Table configuration
        builder.ToTable("TrackedFiles", schema: null);

        // Primary key configuration - Hash as the primary key
        builder.HasKey(t => t.Hash);
        
        builder.Property(t => t.Hash)
            .HasColumnName("Hash")
            .HasColumnType("varchar(64)")
            .HasMaxLength(64)
            .IsRequired()
            .HasComment("SHA256 hash of the file content, serves as unique identifier");

        // File information properties
        builder.Property(t => t.FileName)
            .HasColumnName("FileName")
            .HasColumnType("varchar(500)")
            .HasMaxLength(500)
            .IsRequired()
            .HasComment("Original filename including extension");

        builder.Property(t => t.OriginalPath)
            .HasColumnName("OriginalPath")
            .HasColumnType("varchar(1000)")
            .HasMaxLength(1000)
            .IsRequired()
            .HasComment("Full path where the file was originally discovered");

        builder.Property(t => t.FileSize)
            .HasColumnName("FileSize")
            .HasColumnType("bigint")
            .IsRequired()
            .HasComment("File size in bytes");

        // Processing state configuration
        builder.Property(t => t.Status)
            .HasColumnName("Status")
            .HasColumnType("integer")
            .IsRequired()
            .HasDefaultValue(FileStatus.New)
            .HasConversion<int>()
            .HasComment("Current processing status of the file");

        // ML classification results
        builder.Property(t => t.SuggestedCategory)
            .HasColumnName("SuggestedCategory")
            .HasColumnType("varchar(200)")
            .HasMaxLength(200)
            .IsRequired(false)
            .HasComment("Category suggested by ML classification");

        builder.Property(t => t.Confidence)
            .HasColumnName("Confidence")
            .HasColumnType("decimal(5,4)")
            .HasPrecision(5, 4)
            .IsRequired()
            .HasDefaultValue(0.0m)
            .HasComment("ML classification confidence score (0.0 to 1.0)");

        // User decisions and file organization
        builder.Property(t => t.Category)
            .HasColumnName("Category")
            .HasColumnType("varchar(200)")
            .HasMaxLength(200)
            .IsRequired(false)
            .HasComment("Final category confirmed by user or system");

        builder.Property(t => t.TargetPath)
            .HasColumnName("TargetPath")
            .HasColumnType("varchar(1000)")
            .HasMaxLength(1000)
            .IsRequired(false)
            .HasComment("Target path for file organization");

        // Timestamps for workflow tracking
        builder.Property(t => t.ClassifiedAt)
            .HasColumnName("ClassifiedAt")
            .HasColumnType("datetime")
            .IsRequired(false)
            .HasComment("UTC timestamp when ML classification was completed");

        builder.Property(t => t.MovedAt)
            .HasColumnName("MovedAt")
            .HasColumnType("datetime")
            .IsRequired(false)
            .HasComment("UTC timestamp when file was successfully moved");

        // Error tracking and retry logic
        builder.Property(t => t.LastError)
            .HasColumnName("LastError")
            .HasColumnType("text")
            .IsRequired(false)
            .HasComment("Most recent error message encountered during processing");

        builder.Property(t => t.LastErrorAt)
            .HasColumnName("LastErrorAt")
            .HasColumnType("datetime")
            .IsRequired(false)
            .HasComment("UTC timestamp of the most recent error");

        builder.Property(t => t.RetryCount)
            .HasColumnName("RetryCount")
            .HasColumnType("integer")
            .IsRequired()
            .HasDefaultValue(0)
            .HasComment("Number of processing retry attempts made");

        // Configure indexes for optimized querying patterns
        ConfigureTrackedFileIndexes(builder);
    }

    /// <summary>
    /// Configures indexes optimized for MediaButler's primary query patterns.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
    /// <remarks>
    /// Indexes are designed to support:
    /// - Status-based queries (pending files, errors, etc.)
    /// - ML classification workflows
    /// - File organization operations  
    /// - Error monitoring and retry logic
    /// - Performance analytics and reporting
    /// </remarks>
    private static void ConfigureTrackedFileIndexes(EntityTypeBuilder<TrackedFile> builder)
    {
        // Primary workflow index - Status with IsActive for main processing queries
        builder.HasIndex(t => new { t.Status, t.IsActive })
            .HasDatabaseName("IX_TrackedFiles_Status_IsActive")
            .HasFilter("[IsActive] = 1");

        // ML classification workflow index
        builder.HasIndex(t => new { t.Status, t.Confidence, t.ClassifiedAt })
            .HasDatabaseName("IX_TrackedFiles_Classification_Workflow")
            .HasFilter("[Status] = 2"); // FileStatus.Classified

        // File organization workflow index
        builder.HasIndex(t => new { t.Status, t.Category, t.MovedAt })
            .HasDatabaseName("IX_TrackedFiles_Organization_Workflow")
            .HasFilter("[Status] IN (3, 4, 5)"); // ReadyToMove, Moving, Moved

        // Error monitoring and retry index
        builder.HasIndex(t => new { t.Status, t.RetryCount, t.LastErrorAt })
            .HasDatabaseName("IX_TrackedFiles_Error_Monitoring")
            .HasFilter("[Status] IN (6, 7)"); // Error, Retry

        // Performance analytics - file size and processing time correlation
        builder.HasIndex(t => new { t.FileSize, t.Status, t.CreatedDate })
            .HasDatabaseName("IX_TrackedFiles_Performance_Analytics");

        // Category-based queries for statistics and organization
        builder.HasIndex(t => new { t.Category, t.Status, t.MovedAt })
            .HasDatabaseName("IX_TrackedFiles_Category_Stats")
            .HasFilter("[Category] IS NOT NULL");

        // Original path lookup for duplicate detection
        builder.HasIndex(t => t.OriginalPath)
            .HasDatabaseName("IX_TrackedFiles_OriginalPath");

        // Filename pattern analysis for ML improvement
        builder.HasIndex(t => new { t.FileName, t.Category, t.Confidence })
            .HasDatabaseName("IX_TrackedFiles_Filename_Analysis")
            .HasFilter("[Category] IS NOT NULL AND [Confidence] > 0");
    }
}