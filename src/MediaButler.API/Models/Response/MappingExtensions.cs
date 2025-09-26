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


}