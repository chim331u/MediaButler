using MediaButler.Web.Models;
using System.Text;
using System.Text.Json;

namespace MediaButler.Web.Services.Search;

/// <summary>
/// Service implementation for exporting search results to various formats
/// </summary>
public class SearchExportService : ISearchExportService
{
    private readonly ILogger<SearchExportService> _logger;

    public SearchExportService(ILogger<SearchExportService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<byte[]> ExportToCsvAsync(List<TrackedFileModel> files, SearchCriteriaModel criteria, CancellationToken cancellationToken = default)
    {
        try
        {
            var csv = new StringBuilder();
            
            // Add header with export info
            csv.AppendLine($"# MediaButler Search Results Export");
            csv.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            csv.AppendLine($"# Search Criteria: {criteria.GetDescription()}");
            csv.AppendLine($"# Total Results: {files.Count}");
            csv.AppendLine();
            
            // CSV headers
            csv.AppendLine("Filename,Category,Status,Size (MB),Created Date,File Path,Hash,Confidence,Quality,Extension,Notes");
            
            // Data rows
            foreach (var file in files)
            {
                var filename = EscapeCsvField(file.Filename);
                var category = EscapeCsvField(file.Category ?? "");
                var status = EscapeCsvField(file.Status);
                var sizeMB = file.SizeBytes > 0 ? (file.SizeBytes / 1024.0 / 1024.0).ToString("F2") : "0";
                var createdDate = file.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss");
                var filePath = EscapeCsvField(file.OriginalPath);
                var hash = file.Hash;
                var confidence = file.Confidence?.ToString("F1") ?? "";
                var quality = EscapeCsvField(file.Quality ?? "");
                var extension = EscapeCsvField(file.FileExtension ?? "");
                var notes = EscapeCsvField(file.Note ?? "");
                
                csv.AppendLine($"{filename},{category},{status},{sizeMB},{createdDate},{filePath},{hash},{confidence},{quality},{extension},{notes}");
            }
            
            return Encoding.UTF8.GetBytes(csv.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting search results to CSV");
            throw;
        }
    }

    public async Task<byte[]> ExportToJsonAsync(List<TrackedFileModel> files, SearchCriteriaModel criteria, CancellationToken cancellationToken = default)
    {
        try
        {
            var exportData = new
            {
                ExportInfo = new
                {
                    GeneratedAt = DateTime.UtcNow,
                    SearchCriteria = criteria,
                    TotalResults = files.Count,
                    Description = criteria.GetDescription()
                },
                Files = files.Select(f => new
                {
                    f.Hash,
                    f.Filename,
                    f.Category,
                    f.Status,
                    SizeMB = f.SizeBytes > 0 ? Math.Round(f.SizeBytes / 1024.0 / 1024.0, 2) : 0,
                    f.CreatedDate,
                    f.OriginalPath,
                    f.MovedToPath,
                    f.Confidence,
                    f.Quality,
                    f.FileExtension,
                    f.Note,
                    f.LastUpdateDate
                }).ToList()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(exportData, options);
            return Encoding.UTF8.GetBytes(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting search results to JSON");
            throw;
        }
    }

    public async Task<byte[]> ExportToExcelAsync(List<TrackedFileModel> files, SearchCriteriaModel criteria, CancellationToken cancellationToken = default)
    {
        // For now, return CSV format as Excel alternative
        // In a full implementation, this would use a library like EPPlus or ClosedXML
        _logger.LogWarning("Excel export not fully implemented, falling back to CSV format");
        return await ExportToCsvAsync(files, criteria, cancellationToken);
    }

    public List<ExportFormat> GetSupportedFormats()
    {
        return new List<ExportFormat>
        {
            new ExportFormat
            {
                Id = "csv",
                Name = "CSV",
                FileExtension = ".csv",
                MimeType = "text/csv",
                Icon = "file-text",
                Description = "Comma-separated values file, compatible with Excel and other spreadsheet applications"
            },
            new ExportFormat
            {
                Id = "json",
                Name = "JSON",
                FileExtension = ".json",
                MimeType = "application/json",
                Icon = "code",
                Description = "JavaScript Object Notation format, ideal for data processing and APIs"
            },
            new ExportFormat
            {
                Id = "excel",
                Name = "Excel",
                FileExtension = ".xlsx",
                MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                Icon = "file-spreadsheet",
                Description = "Microsoft Excel format with advanced formatting and features"
            }
        };
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return "";

        // If field contains comma, quote, or newline, wrap in quotes and escape quotes
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}