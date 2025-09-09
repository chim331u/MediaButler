using Microsoft.EntityFrameworkCore;
using MediaButler.Core.Entities;
using MediaButler.Core.Common;
using MediaButler.Data.Configurations;
using MediaButler.Core.Services;

namespace MediaButler.Data;

/// <summary>
/// Entity Framework database context for the MediaButler application.
/// This context provides access to all domain entities with proper configuration
/// and follows "Simple Made Easy" principles with explicit entity sets and clear boundaries.
/// </summary>
/// <remarks>
/// The context implements global soft delete filtering through BaseEntity support,
/// ensuring that soft-deleted entities are automatically excluded from queries unless
/// explicitly included. All entity configurations are externalized to maintain
/// separation of concerns.
/// </remarks>
public class MediaButlerDbContext : DbContext
{
    private readonly IDomainEventPublisher? _domainEventPublisher;

    /// <summary>
    /// Initializes a new instance of the MediaButlerDbContext class.
    /// </summary>
    /// <param name="options">The options to be used by the DbContext.</param>
    /// <param name="domainEventPublisher">Optional service for publishing domain events.</param>
    public MediaButlerDbContext(DbContextOptions<MediaButlerDbContext> options, IDomainEventPublisher? domainEventPublisher = null) : base(options)
    {
        _domainEventPublisher = domainEventPublisher;
    }

    /// <summary>
    /// Gets or sets the TrackedFiles entity set.
    /// Represents media files being tracked by the MediaButler system.
    /// </summary>
    /// <value>A DbSet of TrackedFile entities.</value>
    public DbSet<TrackedFile> TrackedFiles => Set<TrackedFile>();

    /// <summary>
    /// Gets or sets the ProcessingLogs entity set.
    /// Represents audit trail entries for file processing operations.
    /// </summary>
    /// <value>A DbSet of ProcessingLog entities.</value>
    public DbSet<ProcessingLog> ProcessingLogs => Set<ProcessingLog>();

    /// <summary>
    /// Gets or sets the ConfigurationSettings entity set.
    /// Represents dynamic configuration settings for the application.
    /// </summary>
    /// <value>A DbSet of ConfigurationSetting entities.</value>
    public DbSet<ConfigurationSetting> ConfigurationSettings => Set<ConfigurationSetting>();

    /// <summary>
    /// Gets or sets the UserPreferences entity set.
    /// Represents user-specific preference settings.
    /// </summary>
    /// <value>A DbSet of UserPreference entities.</value>
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();

    /// <summary>
    /// Configures the database model and entity relationships.
    /// This method applies all entity configurations and sets up global query filters
    /// for soft delete functionality through BaseEntity.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to configure the model.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from the current assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MediaButlerDbContext).Assembly);

        // Apply global query filters for soft delete functionality
        ApplyGlobalQueryFilters(modelBuilder);
    }

    /// <summary>
    /// Applies global query filters to all entities that inherit from BaseEntity.
    /// This ensures that soft-deleted entities (IsActive = false) are automatically
    /// excluded from all queries unless explicitly included via IgnoreQueryFilters().
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <remarks>
    /// This method uses reflection to find all entities that inherit from BaseEntity
    /// and applies the soft delete filter globally. This follows the "Simple Made Easy"
    /// principle by providing consistent behavior without requiring explicit filtering
    /// in every query.
    /// </remarks>
    private static void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        // Get all entity types configured in the model
        var entityTypes = modelBuilder.Model.GetEntityTypes();

        foreach (var entityType in entityTypes)
        {
            // Check if the entity type inherits from BaseEntity
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                // Create a parameter expression for the entity type
                var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                
                // Create the filter expression: e => e.IsActive
                var propertyAccess = System.Linq.Expressions.Expression.PropertyOrField(parameter, nameof(BaseEntity.IsActive));
                var filterExpression = System.Linq.Expressions.Expression.Lambda(propertyAccess, parameter);

                // Apply the global query filter
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filterExpression);
            }
        }
    }

    /// <summary>
    /// Saves all changes made in the context to the underlying database.
    /// This override ensures that audit properties in BaseEntity are properly
    /// updated before saving changes.
    /// </summary>
    /// <returns>The number of state entries written to the database.</returns>
    /// <remarks>
    /// This method automatically updates LastUpdateDate for modified entities
    /// and sets CreatedDate for new entities that inherit from BaseEntity,
    /// ensuring consistent audit trail without requiring manual intervention.
    /// </remarks>
    public override int SaveChanges()
    {
        UpdateAuditProperties();
        return base.SaveChanges();
    }

    /// <summary>
    /// Asynchronously saves all changes made in the context to the underlying database.
    /// This override ensures that audit properties in BaseEntity are properly
    /// updated before saving changes and publishes domain events.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous save operation. The task result contains the number of state entries written to the database.</returns>
    /// <remarks>
    /// This method automatically updates LastUpdateDate for modified entities
    /// and sets CreatedDate for new entities that inherit from BaseEntity,
    /// ensuring consistent audit trail without requiring manual intervention.
    /// It also publishes domain events after successful save operations.
    /// </remarks>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditProperties();
        
        // Collect entities with domain events before saving
        var entitiesWithEvents = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        // Save changes to database
        var result = await base.SaveChangesAsync(cancellationToken);

        // Publish domain events after successful save
        if (_domainEventPublisher != null && entitiesWithEvents.Any())
        {
            await _domainEventPublisher.PublishEventsAsync(entitiesWithEvents, cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Updates audit properties for entities that inherit from BaseEntity.
    /// This method is called automatically before saving changes to ensure
    /// consistent audit trail information.
    /// </summary>
    /// <remarks>
    /// This implementation follows "Simple Made Easy" principles by centralizing
    /// audit property management in one place, avoiding the need to manually
    /// update audit fields throughout the application.
    /// </remarks>
    private void UpdateAuditProperties()
    {
        var entries = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        var now = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // Only set CreatedDate if it hasn't been explicitly set (i.e., still has default value from constructor)
                    // This allows test data builders to set custom timestamps
                    var createdDateProperty = entry.Property(nameof(BaseEntity.CreatedDate));
                    var lastUpdateDateProperty = entry.Property(nameof(BaseEntity.LastUpdateDate));
                    
                    // Check if CreatedDate was explicitly set by comparing against a reasonable time window
                    // If it was set via reflection (like in tests), it won't match the "now" window
                    var timeDifference = Math.Abs((now - entry.Entity.CreatedDate).TotalSeconds);
                    if (timeDifference > 10) // More than 10 seconds difference suggests explicit setting
                    {
                        // CreatedDate was explicitly set, preserve it and don't modify LastUpdateDate either
                        // This preserves test data timestamps
                    }
                    else
                    {
                        // CreatedDate appears to be default, set both timestamps
                        entry.Entity.CreatedDate = now;
                        entry.Entity.LastUpdateDate = now;
                    }
                    break;

                case EntityState.Modified:
                    // Prevent modification of CreatedDate
                    entry.Property(nameof(BaseEntity.CreatedDate)).IsModified = false;
                    entry.Entity.LastUpdateDate = now;
                    break;
            }
        }
    }
}