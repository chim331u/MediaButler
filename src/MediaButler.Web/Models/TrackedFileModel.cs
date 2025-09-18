namespace MediaButler.Web.Models;

/// <summary>
/// UI model for tracked files with display-friendly properties
/// </summary>
public record TrackedFileModel
{
    public string Hash { get; init; } = string.Empty;
    public string Filename { get; init; } = string.Empty;
    public string OriginalPath { get; init; } = string.Empty;
    public string? MovedToPath { get; init; }
    public long SizeBytes { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Category { get; init; }
    public double? Confidence { get; init; }
    public string? Quality { get; init; }
    public string? FileExtension { get; init; }
    public DateTime CreatedDate { get; init; }
    public DateTime LastUpdateDate { get; init; }
    public string? Note { get; init; }
    public bool IsActive { get; init; } = true;

    // Computed properties for UI
    public string DisplaySize => FormatFileSize(SizeBytes);
    public string DisplayConfidence => Confidence?.ToString("F1") + "%" ?? "N/A";
    public string DisplayCategory => Category ?? "Uncategorized";
    public string StatusDisplayClass => Status.ToLowerInvariant() switch
    {
        "new" => "status-new",
        "classified" => "status-classified", 
        "confirmed" => "status-confirmed",
        "moved" => "status-moved",
        "error" => "status-error",
        _ => "status-unknown"
    };

    public bool IsMovable => Status == "Confirmed" && !string.IsNullOrEmpty(Category);
    public bool HasError => Status == "Error";
    public bool IsProcessed => Status == "Moved";

    private static string FormatFileSize(long bytes)
    {
        if (bytes == 0) return "0 B";
        
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        
        return $"{size:F1} {sizes[order]}";
    }
}

/// <summary>
/// Extension methods for converting between Core entities and UI models
/// </summary>
public static class TrackedFileModelExtensions
{
    public static TrackedFileModel ToModel(this MediaButler.Core.Entities.TrackedFile entity)
    {
        return new TrackedFileModel
        {
            Hash = entity.Hash,
            Filename = entity.FileName,
            OriginalPath = entity.OriginalPath,
            MovedToPath = entity.MovedToPath,
            SizeBytes = entity.FileSize,
            Status = entity.Status.ToString(),
            Category = entity.Category,
            Confidence = (double?)entity.Confidence,
            Quality = ExtractQuality(entity.FileName),
            FileExtension = ExtractFileExtension(entity.FileName),
            CreatedDate = entity.CreatedDate,
            LastUpdateDate = entity.LastUpdateDate,
            Note = entity.Note,
            IsActive = entity.IsActive
        };
    }

    private static string? ExtractQuality(string fileName)
    {
        if (fileName.Contains("2160p") || fileName.Contains("4K")) return "4K";
        if (fileName.Contains("1080p")) return "1080p";
        if (fileName.Contains("720p")) return "720p";
        if (fileName.Contains("480p")) return "480p";
        return null;
    }

    private static string? ExtractFileExtension(string fileName)
    {
        var lastDotIndex = fileName.LastIndexOf('.');
        return lastDotIndex >= 0 ? fileName.Substring(lastDotIndex) : null;
    }

    public static IEnumerable<TrackedFileModel> ToModels(this IEnumerable<MediaButler.Core.Entities.TrackedFile> entities)
    {
        return entities.Select(e => e.ToModel());
    }
}