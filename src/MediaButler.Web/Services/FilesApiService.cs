using MediaButler.Web.Interfaces;
using MediaButler.Web.Models;

namespace MediaButler.Web.Services;

/// <summary>
/// Files API service following "Simple Made Easy" principles.
/// Single responsibility: File management operations only.
/// Composes with IHttpClientService without braiding concerns.
/// </summary>
public interface IFilesApiService
{
    /// <summary>
    /// Gets tracked files with pagination and optional filtering.
    /// Pure function - same inputs produce same outputs.
    /// </summary>
    Task<Result<IReadOnlyList<FileManagementDto>>> GetFilesAsync(
        int skip = 0,
        int take = 20,
        string? status = null,
        string? category = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific tracked file by its hash.
    /// </summary>
    Task<Result<FileManagementDto>> GetFileAsync(
        string hash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets files that are awaiting user confirmation after classification.
    /// </summary>
    Task<Result<IReadOnlyList<FileManagementDto>>> GetPendingFilesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets files ready for ML classification processing.
    /// </summary>
    Task<Result<IReadOnlyList<FileManagementDto>>> GetFilesReadyForClassificationAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms a file's category assignment.
    /// </summary>
    Task<Result<FileManagementDto>> ConfirmFileCategoryAsync(
        string hash,
        string category,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a file as moved to its target location.
    /// </summary>
    Task<Result<FileManagementDto>> MarkFileAsMovedAsync(
        string hash,
        string targetPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes a tracked file.
    /// </summary>
    Task<Result> DeleteFileAsync(
        string hash,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually triggers a scan of configured watch folders.
    /// </summary>
    Task<Result<ScanResultDto>> ScanFoldersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually triggers a scan of a specific folder.
    /// </summary>
    Task<Result<ScanResultDto>> ScanSpecificFolderAsync(
        string folderPath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of Files API service.
/// No state - each request is independent.
/// Values over exceptions - returns explicit Results.
/// </summary>
public class FilesApiService : IFilesApiService
{
    private readonly IHttpClientService _httpClient;

    public FilesApiService(IHttpClientService httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Result<IReadOnlyList<FileManagementDto>>> GetFilesAsync(
        int skip = 0,
        int take = 20,
        string? status = null,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queryParams = new List<string>
            {
                $"skip={skip}",
                $"take={take}"
            };

            if (!string.IsNullOrWhiteSpace(status))
                queryParams.Add($"status={Uri.EscapeDataString(status)}");

            if (!string.IsNullOrWhiteSpace(category))
                queryParams.Add($"category={Uri.EscapeDataString(category)}");

            var query = string.Join("&", queryParams);
            var result = await _httpClient.GetAsync<TrackedFileResponse[]>($"/api/files?{query}", cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<IReadOnlyList<FileManagementDto>>.Failure(result.Error, result.StatusCode);
            }

            var files = result.Value?.Select(MapToFileManagementDto).ToList() ?? new List<FileManagementDto>();
            return Result<IReadOnlyList<FileManagementDto>>.Success(files);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<FileManagementDto>>.Failure($"Failed to get files: {ex.Message}");
        }
    }

    public async Task<Result<FileManagementDto>> GetFileAsync(
        string hash,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _httpClient.GetAsync<TrackedFileResponse>($"/api/files/{hash}", cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<FileManagementDto>.Failure(result.Error, result.StatusCode);
            }

            var file = MapToFileManagementDto(result.Value!);
            return Result<FileManagementDto>.Success(file);
        }
        catch (Exception ex)
        {
            return Result<FileManagementDto>.Failure($"Failed to get file: {ex.Message}");
        }
    }

    public async Task<Result<IReadOnlyList<FileManagementDto>>> GetPendingFilesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _httpClient.GetAsync<TrackedFileResponse[]>("/api/files/pending", cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<IReadOnlyList<FileManagementDto>>.Failure(result.Error, result.StatusCode);
            }

            var files = result.Value?.Select(MapToFileManagementDto).ToList() ?? new List<FileManagementDto>();
            return Result<IReadOnlyList<FileManagementDto>>.Success(files);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<FileManagementDto>>.Failure($"Failed to get pending files: {ex.Message}");
        }
    }

    public async Task<Result<IReadOnlyList<FileManagementDto>>> GetFilesReadyForClassificationAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _httpClient.GetAsync<TrackedFileResponse[]>($"/api/files/ready-for-classification?limit={limit}", cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<IReadOnlyList<FileManagementDto>>.Failure(result.Error, result.StatusCode);
            }

            var files = result.Value?.Select(MapToFileManagementDto).ToList() ?? new List<FileManagementDto>();
            return Result<IReadOnlyList<FileManagementDto>>.Success(files);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<FileManagementDto>>.Failure($"Failed to get files ready for classification: {ex.Message}");
        }
    }

    public async Task<Result<FileManagementDto>> ConfirmFileCategoryAsync(
        string hash,
        string category,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new { Category = category };
            var result = await _httpClient.PostAsync<TrackedFileResponse>($"/api/files/{hash}/confirm", request, cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<FileManagementDto>.Failure(result.Error, result.StatusCode);
            }

            var file = MapToFileManagementDto(result.Value!);
            return Result<FileManagementDto>.Success(file);
        }
        catch (Exception ex)
        {
            return Result<FileManagementDto>.Failure($"Failed to confirm file category: {ex.Message}");
        }
    }

    public async Task<Result<FileManagementDto>> MarkFileAsMovedAsync(
        string hash,
        string targetPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new { TargetPath = targetPath };
            var result = await _httpClient.PostAsync<TrackedFileResponse>($"/api/files/{hash}/moved", request, cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<FileManagementDto>.Failure(result.Error, result.StatusCode);
            }

            var file = MapToFileManagementDto(result.Value!);
            return Result<FileManagementDto>.Success(file);
        }
        catch (Exception ex)
        {
            return Result<FileManagementDto>.Failure($"Failed to mark file as moved: {ex.Message}");
        }
    }

    public async Task<Result> DeleteFileAsync(
        string hash,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            object? request = null;
            if (!string.IsNullOrWhiteSpace(reason))
            {
                request = new { Reason = reason };
            }

            var result = await _httpClient.DeleteAsync($"/api/files/{hash}", cancellationToken);

            if (!result.IsSuccess)
            {
                return Result.Failure(result.Error, result.StatusCode);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to delete file: {ex.Message}");
        }
    }

    public async Task<Result<ScanResultDto>> ScanFoldersAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _httpClient.PostAsync<ScanResult>("/api/files/scan", null, cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<ScanResultDto>.Failure(result.Error, result.StatusCode);
            }

            var scanResult = MapToScanResultDto(result.Value!);
            return Result<ScanResultDto>.Success(scanResult);
        }
        catch (Exception ex)
        {
            return Result<ScanResultDto>.Failure($"Failed to scan folders: {ex.Message}");
        }
    }

    public async Task<Result<ScanResultDto>> ScanSpecificFolderAsync(
        string folderPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new { FolderPath = folderPath };
            var result = await _httpClient.PostAsync<ScanResult>("/api/files/scan/folder", request, cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<ScanResultDto>.Failure(result.Error, result.StatusCode);
            }

            var scanResult = MapToScanResultDto(result.Value!);
            return Result<ScanResultDto>.Success(scanResult);
        }
        catch (Exception ex)
        {
            return Result<ScanResultDto>.Failure($"Failed to scan specific folder: {ex.Message}");
        }
    }

    /// <summary>
    /// Maps API response to FileManagementDto.
    /// Pure function - deterministic mapping logic.
    /// </summary>
    private static FileManagementDto MapToFileManagementDto(TrackedFileResponse file)
    {
        return new FileManagementDto
        {
            Id = file.Hash.GetHashCode(), // Generate ID from hash since API doesn't return numeric ID
            Name = file.FileName,
            FileSize = file.FileSize,
            FileCategory = file.Category ?? file.SuggestedCategory,
            IsToCategorize = file.Status == 2, // Status 2 = "Ready for review"
            IsNotToMove = file.Status == 6 || file.Status == 5, // Error or Moved status
            IsNew = file.Status == 0 || file.Status == 1, // New or Processing status
            Hash = file.Hash,
            OriginalPath = file.OriginalPath,
            TargetPath = file.TargetPath,
            CreatedDate = file.CreatedAt,
            ClassifiedAt = file.ClassifiedAt,
            Confidence = (decimal)(file.ConfidencePercentage ?? 0),
            Status = file.StatusDescription ?? $"Status {file.Status}"
        };
    }

    /// <summary>
    /// Maps API response to ScanResultDto.
    /// Pure function - deterministic mapping logic.
    /// </summary>
    private static ScanResultDto MapToScanResultDto(ScanResult scanResult)
    {
        return new ScanResultDto
        {
            FilesDiscovered = scanResult.FilesDiscovered,
            ScanStartedAt = scanResult.ScanStartedAt,
            ScanCompletedAt = scanResult.ScanCompletedAt,
            MonitoringEnabled = scanResult.MonitoringEnabled,
            MonitoredPaths = scanResult.MonitoredPaths,
            ScannedPath = scanResult.ScannedPath,
            ScanDurationMs = scanResult.ScanDurationMs
        };
    }
}

/// <summary>
/// DTO for API response mapping
/// </summary>
public class TrackedFileResponse
{
    public required string Hash { get; set; }
    public required string FileName { get; set; }
    public required string OriginalPath { get; set; }
    public long FileSize { get; set; }
    public string? FormattedFileSize { get; set; }
    public int Status { get; set; }
    public string? StatusDescription { get; set; }
    public string? SuggestedCategory { get; set; }
    public double? ConfidencePercentage { get; set; }
    public string? ConfidenceLevel { get; set; }
    public string? Category { get; set; }
    public string? TargetPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ClassifiedAt { get; set; }
    public DateTime? MovedAt { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastErrorAt { get; set; }
    public int RetryCount { get; set; }
    public bool RequiresAttention { get; set; }
    public double ProcessingDurationMs { get; set; }
}

/// <summary>
/// DTO for scan result mapping
/// </summary>
public class ScanResult
{
    public int FilesDiscovered { get; set; }
    public DateTime ScanStartedAt { get; set; }
    public DateTime ScanCompletedAt { get; set; }
    public bool MonitoringEnabled { get; set; }
    public List<string> MonitoredPaths { get; set; } = new();
    public string? ScannedPath { get; set; }
    public double ScanDurationMs { get; set; }
}

/// <summary>
/// DTO for scan result in the web layer
/// </summary>
public class ScanResultDto
{
    public int FilesDiscovered { get; set; }
    public DateTime ScanStartedAt { get; set; }
    public DateTime ScanCompletedAt { get; set; }
    public bool MonitoringEnabled { get; set; }
    public List<string> MonitoredPaths { get; set; } = new();
    public string? ScannedPath { get; set; }
    public double ScanDurationMs { get; set; }
}