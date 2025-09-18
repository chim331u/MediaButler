using MediaButler.Web.Models;

namespace MediaButler.Web.Services.Search;

/// <summary>
/// Service for exporting search results to various formats
/// </summary>
public interface ISearchExportService
{
    /// <summary>
    /// Exports search results to CSV format
    /// </summary>
    /// <param name="files">Files to export</param>
    /// <param name="criteria">Search criteria used</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>CSV content as byte array</returns>
    Task<byte[]> ExportToCsvAsync(List<TrackedFileModel> files, SearchCriteriaModel criteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports search results to JSON format
    /// </summary>
    /// <param name="files">Files to export</param>
    /// <param name="criteria">Search criteria used</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JSON content as byte array</returns>
    Task<byte[]> ExportToJsonAsync(List<TrackedFileModel> files, SearchCriteriaModel criteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports search results to Excel format
    /// </summary>
    /// <param name="files">Files to export</param>
    /// <param name="criteria">Search criteria used</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Excel content as byte array</returns>
    Task<byte[]> ExportToExcelAsync(List<TrackedFileModel> files, SearchCriteriaModel criteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets supported export formats
    /// </summary>
    /// <returns>List of supported export formats</returns>
    List<ExportFormat> GetSupportedFormats();
}

/// <summary>
/// Represents an export format option
/// </summary>
public record ExportFormat
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string FileExtension { get; init; } = string.Empty;
    public string MimeType { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Export progress information
/// </summary>
public record ExportProgressModel
{
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public string CurrentStatus { get; init; } = string.Empty;
    public bool IsCompleted { get; init; }
    public bool HasError { get; init; }
    public string? ErrorMessage { get; init; }
    
    public double ProgressPercentage => TotalFiles > 0 ? (double)ProcessedFiles / TotalFiles * 100 : 0;
}