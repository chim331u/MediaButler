using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MediaButler.Core.Entities;
using MediaButler.Core.Models;
using MediaButler.Core.Services;

namespace MediaButler.Data.Configurations;

/// <summary>
/// Entity configuration for FileOrganizationStateEntity.
/// Defines database schema, indexes, and constraints for organization state tracking.
/// </summary>
/// <remarks>
/// This configuration optimizes the FileOrganizationStates table for:
/// - Fast lookup by file hash (unique index)
/// - Efficient state-based queries (for monitoring)
/// - Stale state cleanup (for background maintenance)
/// </remarks>
public class FileOrganizationStateConfiguration : BaseEntityConfiguration<FileOrganizationStateEntity>
{
    protected override void ConfigureEntity(EntityTypeBuilder<FileOrganizationStateEntity> builder)
    {
        // Table configuration
        builder.ToTable("FileOrganizationStates", schema: null);

        // Primary key
        builder.HasKey(s => s.Id);

        // Properties
        builder.Property(s => s.FileHash)
            .HasColumnName("FileHash")
            .HasColumnType("varchar(64)")
            .HasMaxLength(64)
            .IsRequired()
            .HasComment("SHA256 hash of the file being organized");

        builder.Property(s => s.State)
            .HasColumnName("State")
            .HasColumnType("integer")
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(OrganizationState.Pending)
            .HasComment("Current state of the organization operation");

        builder.Property(s => s.StateContext)
            .HasColumnName("StateContext")
            .HasColumnType("text")
            .IsRequired(false)
            .HasComment("Optional additional context about the current state");

        builder.Property(s => s.StateUpdatedAt)
            .HasColumnName("StateUpdatedAt")
            .HasColumnType("datetime")
            .IsRequired()
            .HasComment("UTC timestamp when the state was last updated");

        // Indexes
        ConfigureIndexes(builder);
    }

    private static void ConfigureIndexes(EntityTypeBuilder<FileOrganizationStateEntity> builder)
    {
        // Unique index on FileHash for fast lookup (one state per file)
        builder.HasIndex(s => s.FileHash)
            .IsUnique()
            .HasDatabaseName("IX_FileOrganizationStates_FileHash_Unique")
            .HasFilter("[IsActive] = 1");

        // State-based queries for monitoring
        builder.HasIndex(s => new { s.State, s.StateUpdatedAt })
            .HasDatabaseName("IX_FileOrganizationStates_State_UpdatedAt")
            .HasFilter("[IsActive] = 1");

        // Stale state cleanup (files stuck in InProgress)
        builder.HasIndex(s => new { s.State, s.StateUpdatedAt, s.IsActive })
            .HasDatabaseName("IX_FileOrganizationStates_StaleCleanup")
            .HasFilter("[State] = 1 AND [IsActive] = 1"); // OrganizationState.InProgress
    }
}
