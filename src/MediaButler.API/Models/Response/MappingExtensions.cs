using MediaButler.Core.Entities;
using MediaButler.Core.Enums;

namespace MediaButler.API.Models.Response;

/// <summary>
/// Extension methods for mapping domain entities to API response DTOs.
/// Follows "Simple Made Easy" principles by separating mapping concerns
/// from business logic and providing explicit, single-purpose transformations.
/// </summary>
/// <remarks>
/// These extension methods provide a clean separation between domain entities
/// and API contracts, allowing each to evolve independently. The mappings
/// handle type conversions, formatting, and computed properties needed for
/// client consumption without complecting domain logic with presentation concerns.
/// </remarks>
public static class MappingExtensions
{
    /// <summary>
    /// Maps a TrackedFile domain entity to a TrackedFileResponse DTO.
    /// Handles all property transformations, formatting, and computed values
    /// needed for API consumption.
    /// </summary>
    /// <param name="entity">The TrackedFile entity to map.</param>
    /// <returns>A TrackedFileResponse DTO with all properties mapped and formatted.</returns>
    /// <exception cref="ArgumentNullException">Thrown when entity is null.</exception>
    public static TrackedFileResponse ToResponse(this TrackedFile entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        var response = new TrackedFileResponse
        {
            Hash = entity.Hash,
            FileName = entity.FileName,
            OriginalPath = entity.OriginalPath,
            FileSize = entity.FileSize,
            FormattedFileSize = TrackedFileResponse.FormatFileSize(entity.FileSize),
            Status = entity.Status,
            StatusDescription = TrackedFileResponse.GetStatusDescription(entity.Status),
            SuggestedCategory = entity.SuggestedCategory,
            Category = entity.Category,
            TargetPath = entity.TargetPath,
            CreatedAt = entity.CreatedDate,
            UpdatedAt = entity.LastUpdateDate,
            ClassifiedAt = entity.ClassifiedAt,
            MovedAt = entity.MovedAt,
            LastError = entity.LastError,
            LastErrorAt = entity.LastErrorAt,
            RetryCount = entity.RetryCount
        };

        // Calculate confidence percentage and level
        if (entity.Confidence > 0)
        {
            response.ConfidencePercentage = entity.Confidence * 100;
            response.ConfidenceLevel = TrackedFileResponse.GetConfidenceLevel(response.ConfidencePercentage);
        }

        // Determine if file requires user attention
        response.RequiresAttention = entity.Status switch
        {
            FileStatus.Classified => true,
            FileStatus.Error => true,
            FileStatus.Retry when entity.RetryCount > 2 => true,
            _ => false
        };

        // Calculate processing duration if available
        if (entity.ClassifiedAt.HasValue)
        {
            var duration = entity.ClassifiedAt.Value - entity.CreatedDate;
            response.ProcessingDurationMs = (long)duration.TotalMilliseconds;
        }

        return response;
    }

    /// <summary>
    /// Maps a ConfigurationSetting domain entity to a ConfigurationResponse DTO.
    /// Handles value deserialization, type conversion, and user-friendly formatting.
    /// </summary>
    /// <param name="entity">The ConfigurationSetting entity to map.</param>
    /// <returns>A ConfigurationResponse DTO with all properties mapped.</returns>
    /// <exception cref="ArgumentNullException">Thrown when entity is null.</exception>
    public static ConfigurationResponse ToResponse(this ConfigurationSetting entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        var response = new ConfigurationResponse
        {
            Key = entity.Key,
            RawValue = entity.Value,
            Section = entity.Section,
            Description = entity.Description,
            DataType = entity.DataType,
            DataTypeDescription = ConfigurationResponse.GetDataTypeDescription(entity.DataType),
            RequiresRestart = entity.RequiresRestart,
            CreatedAt = entity.CreatedDate,
            UpdatedAt = entity.LastUpdateDate
        };

        // Deserialize the JSON value to the appropriate type
        response.Value = DeserializeConfigurationValue(entity.Value, entity.DataType);

        // Add validation rules based on data type
        response.ValidationRules = GetValidationRules(entity.DataType);

        return response;
    }

    /// <summary>
    /// Maps a collection of TrackedFile entities to a paginated response.
    /// Combines entity mapping with pagination metadata.
    /// </summary>
    /// <param name="entities">The collection of TrackedFile entities.</param>
    /// <param name="page">The current page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="totalCount">The total number of items available.</param>
    /// <returns>A paginated response containing mapped TrackedFileResponse DTOs.</returns>
    public static PagedResponse<TrackedFileResponse> ToPagedResponse(
        this IEnumerable<TrackedFile> entities,
        int page,
        int pageSize,
        int totalCount)
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));

        var mappedEntities = entities.Select(e => e.ToResponse()).ToList();
        return PagedResponse<TrackedFileResponse>.Create(mappedEntities, page, pageSize, totalCount);
    }

    /// <summary>
    /// Maps a collection of ConfigurationSetting entities to configuration sections.
    /// Groups settings by section and provides organized structure for display.
    /// </summary>
    /// <param name="entities">The collection of ConfigurationSetting entities.</param>
    /// <returns>A ConfigurationSummary with organized sections.</returns>
    public static ConfigurationSummary ToConfigurationSummary(this IEnumerable<ConfigurationSetting> entities)
    {
        if (entities == null)
            throw new ArgumentNullException(nameof(entities));

        var groupedSettings = entities
            .Select(e => e.ToResponse())
            .GroupBy(r => r.Section)
            .Select(g => new ConfigurationSection
            {
                Name = g.Key,
                Title = FormatSectionTitle(g.Key),
                Description = GetSectionDescription(g.Key),
                Settings = g.OrderBy(s => s.Key).ToList(),
                Order = GetSectionOrder(g.Key)
            })
            .OrderBy(s => s.Order)
            .ToList();

        return new ConfigurationSummary
        {
            Sections = groupedSettings,
            GeneratedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Deserializes a JSON configuration value to its appropriate .NET type.
    /// </summary>
    /// <param name="jsonValue">The JSON string value from the database.</param>
    /// <param name="dataType">The expected data type.</param>
    /// <returns>The deserialized value in its appropriate type.</returns>
    private static object? DeserializeConfigurationValue(string jsonValue, ConfigurationDataType dataType)
    {
        if (string.IsNullOrWhiteSpace(jsonValue))
            return null;

        try
        {
            return dataType switch
            {
                ConfigurationDataType.Integer => System.Text.Json.JsonSerializer.Deserialize<int>(jsonValue),
                ConfigurationDataType.Boolean => System.Text.Json.JsonSerializer.Deserialize<bool>(jsonValue),
                ConfigurationDataType.String => System.Text.Json.JsonSerializer.Deserialize<string>(jsonValue),
                ConfigurationDataType.Path => System.Text.Json.JsonSerializer.Deserialize<string>(jsonValue),
                ConfigurationDataType.Json => System.Text.Json.JsonSerializer.Deserialize<object>(jsonValue),
                _ => jsonValue
            };
        }
        catch (System.Text.Json.JsonException)
        {
            // Return raw value if JSON deserialization fails
            return jsonValue;
        }
    }

    /// <summary>
    /// Gets validation rules for a specific configuration data type.
    /// </summary>
    /// <param name="dataType">The configuration data type.</param>
    /// <returns>A collection of validation rules for the data type.</returns>
    private static IReadOnlyCollection<ValidationRule> GetValidationRules(ConfigurationDataType dataType)
    {
        return dataType switch
        {
            ConfigurationDataType.Integer => new[]
            {
                new ValidationRule
                {
                    Type = "type",
                    Parameters = new Dictionary<string, object> { { "type", "integer" } },
                    ErrorMessage = "Value must be a valid integer"
                }
            },
            ConfigurationDataType.Boolean => new[]
            {
                new ValidationRule
                {
                    Type = "type",
                    Parameters = new Dictionary<string, object> { { "type", "boolean" } },
                    ErrorMessage = "Value must be true or false"
                }
            },
            ConfigurationDataType.Path => new[]
            {
                new ValidationRule
                {
                    Type = "path",
                    Parameters = new Dictionary<string, object>(),
                    ErrorMessage = "Value must be a valid file system path"
                }
            },
            ConfigurationDataType.Json => new[]
            {
                new ValidationRule
                {
                    Type = "json",
                    Parameters = new Dictionary<string, object>(),
                    ErrorMessage = "Value must be valid JSON"
                }
            },
            _ => Array.Empty<ValidationRule>()
        };
    }

    /// <summary>
    /// Formats a section name into a user-friendly title.
    /// </summary>
    /// <param name="sectionName">The section name.</param>
    /// <returns>A formatted title for display.</returns>
    private static string FormatSectionTitle(string sectionName)
    {
        return sectionName switch
        {
            "ML" => "Machine Learning",
            "Paths" => "File Paths",
            "Butler" => "File Butler Settings",
            "Database" => "Database Configuration",
            "Performance" => "Performance Settings",
            _ => sectionName.Replace("_", " ").Replace("-", " ")
        };
    }

    /// <summary>
    /// Gets a description for a configuration section.
    /// </summary>
    /// <param name="sectionName">The section name.</param>
    /// <returns>A description of the section's purpose.</returns>
    private static string GetSectionDescription(string sectionName)
    {
        return sectionName switch
        {
            "ML" => "Machine learning classification and model settings",
            "Paths" => "File system paths for input, output, and processing",
            "Butler" => "Automated file processing and organization behavior",
            "Database" => "Database connection and performance settings",
            "Performance" => "System performance monitoring and optimization",
            _ => $"Settings for {sectionName.ToLower()} functionality"
        };
    }

    /// <summary>
    /// Gets the display order for a configuration section.
    /// </summary>
    /// <param name="sectionName">The section name.</param>
    /// <returns>An integer indicating display order (lower numbers first).</returns>
    private static int GetSectionOrder(string sectionName)
    {
        return sectionName switch
        {
            "Paths" => 1,
            "Butler" => 2,
            "ML" => 3,
            "Performance" => 4,
            "Database" => 5,
            _ => 99
        };
    }
}