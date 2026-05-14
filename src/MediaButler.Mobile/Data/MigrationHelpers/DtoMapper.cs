using MediaButler.Mobile.Data;
using MediaButler.Mobile.Models;
using MediaButler.Shared.UI.Models;

namespace MediaButler.Mobile.Data.MigrationHelpers;

/// <summary>
/// Bidirectional mapper between legacy and new DTOs.
/// Enables gradual migration from FilesDetailDto to FileManagementDto.
/// Following "Simple Made Easy" - explicit mapping without magic.
/// </summary>
public static class DtoMapper
{
    /// <summary>
    /// Converts legacy FilesDetailDto to new FileManagementDto.
    /// Used when calling new API services.
    /// </summary>
    public static FileManagementDto ToFileManagementDto(FilesDetailDto legacy)
    {
        ArgumentNullException.ThrowIfNull(legacy);

        return new FileManagementDto
        {
            Id = legacy.Id,
            Name = legacy.Name,
            FileSize = (long)legacy.FileSize,
            FileCategory = legacy.FileCategory,
            Hash = GenerateTemporaryHash(legacy.Id), // Temporary: generate hash from ID
            Status = DeriveStatusFromFlags(legacy),

            // New fields - defaults (will be populated by API)
            OriginalPath = null,
            TargetPath = null,
            CreatedDate = null,
            ClassifiedAt = null,
            Confidence = 0
        };
    }

    /// <summary>
    /// Converts new FileManagementDto to legacy FilesDetailDto.
    /// Used for backward compatibility with existing UI components.
    /// </summary>
    public static FilesDetailDto ToFilesDetailDto(FileManagementDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new FilesDetailDto
        {
            Id = dto.Id,
            Name = dto.Name,
            FileSize = dto.FileSize,
            FileCategory = dto.FileCategory,

            // Derive legacy boolean flags from new Status field
            IsToCategorize = dto.IsToCategorize,
            IsNew = dto.IsNew,
            IsNotToMove = dto.IsNotToMove
        };
    }

    /// <summary>
    /// Converts a collection of FileManagementDto to FilesDetailDto.
    /// </summary>
    public static List<FilesDetailDto> ToFilesDetailDtoList(IEnumerable<FileManagementDto> dtos)
    {
        ArgumentNullException.ThrowIfNull(dtos);
        return dtos.Select(ToFilesDetailDto).ToList();
    }

    /// <summary>
    /// Converts a collection of FilesDetailDto to FileManagementDto.
    /// </summary>
    public static List<FileManagementDto> ToFileManagementDtoList(IEnumerable<FilesDetailDto> legacyDtos)
    {
        ArgumentNullException.ThrowIfNull(legacyDtos);
        return legacyDtos.Select(ToFileManagementDto).ToList();
    }

    /// <summary>
    /// Derives file status from legacy boolean flags.
    /// Best-effort mapping - may not be 100% accurate.
    /// </summary>
    private static string DeriveStatusFromFlags(FilesDetailDto legacy)
    {
        // Priority order based on most specific flags
        if (legacy.IsNotToMove)
            return "Moved"; // Assume moved if marked not to move

        if (legacy.IsToCategorize)
            return "Classified"; // Ready for user confirmation

        if (legacy.IsNew)
            return "New"; // Just discovered

        return "Unknown"; // Fallback
    }

    /// <summary>
    /// Generates a temporary hash from legacy ID.
    /// Format: "legacy_{id}" to distinguish from real SHA256 hashes.
    /// Will be replaced with real hash after full migration.
    /// </summary>
    private static string GenerateTemporaryHash(int id)
    {
        return $"legacy_{id}";
    }

    /// <summary>
    /// Checks if a hash is a temporary legacy hash.
    /// </summary>
    public static bool IsTemporaryHash(string? hash)
    {
        return hash?.StartsWith("legacy_", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    /// <summary>
    /// Extracts the original ID from a temporary hash.
    /// Returns null if not a temporary hash.
    /// </summary>
    public static int? ExtractIdFromTemporaryHash(string? hash)
    {
        if (string.IsNullOrWhiteSpace(hash) || !IsTemporaryHash(hash))
            return null;

        var idPart = hash.Substring(7); // Remove "legacy_" prefix
        return int.TryParse(idPart, out var id) ? id : null;
    }
}
