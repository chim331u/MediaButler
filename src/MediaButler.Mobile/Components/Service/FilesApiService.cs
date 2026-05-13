using MediaButler.Core.Enums;
using MediaButler.Mobile.Components.Interfaces;
using MediaButler.Mobile.Models;

namespace MediaButler.Mobile.Components.Service;

/// <summary>
/// Files API service following "Simple Made Easy" principles.
/// Single responsibility: File management operations only.
/// Composes with IHttpClientService without braiding concerns.
/// </summary>

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
                return Result<IReadOnlyList<FileManagementDto>>.Failure(result.Error, result.StatusCode ?? 0);
            }

            var files = result.Value?.Select(MapToFileManagementDto).ToList() ?? new List<FileManagementDto>();
            return Result<IReadOnlyList<FileManagementDto>>.Success(files);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<FileManagementDto>>.Failure($"Failed to get files: {ex.Message}");
        }
    }

    public async Task<Result<PaginatedFilesDto>> GetFilesByStatusesAsync(
        int skip = 0,
        int take = 20,
        FileStatus[] statuses = null!,
        string? category = null,
        string? searchTerm = null,
        string? orderBy = null,
        bool descending = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queryParams = new List<string>
            {
                $"skip={skip}",
                $"take={take}",
                $"descending={descending.ToString().ToLower()}"
            };

            if (statuses != null && statuses.Length > 0)
            {
                foreach (var status in statuses)
                {
                    queryParams.Add($"statuses={Uri.EscapeDataString(status.ToString())}");
                }
            }

            if (!string.IsNullOrWhiteSpace(category))
                queryParams.Add($"category={Uri.EscapeDataString(category)}");

            if (!string.IsNullOrWhiteSpace(searchTerm))
                queryParams.Add($"searchTerm={Uri.EscapeDataString(searchTerm)}");

            if (!string.IsNullOrWhiteSpace(orderBy))
                queryParams.Add($"orderBy={Uri.EscapeDataString(orderBy)}");

            var query = string.Join("&", queryParams);
            var result = await _httpClient.GetAsync<PaginatedFilesResponse>($"/api/files/by-statuses?{query}", cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<PaginatedFilesDto>.Failure(result.Error, result.StatusCode ?? 0);
            }

            var response = result.Value;
            if (response == null)
            {
                return Result<PaginatedFilesDto>.Failure("Empty response from API");
            }

            var paginatedDto = new PaginatedFilesDto
            {
                Items = response.Items?.Select(MapToFileManagementDto).ToList() ?? new List<FileManagementDto>(),
                Total = response.Total,
                Skip = response.Skip,
                Take = response.Take
            };

            return Result<PaginatedFilesDto>.Success(paginatedDto);
        }
        catch (Exception ex)
        {
            return Result<PaginatedFilesDto>.Failure($"Failed to get files by statuses: {ex.Message}");
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
                return Result<FileManagementDto>.Failure(result.Error, result.StatusCode ?? 0);
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
                return Result<IReadOnlyList<FileManagementDto>>.Failure(result.Error, result.StatusCode ?? 0);
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
                return Result<IReadOnlyList<FileManagementDto>>.Failure(result.Error, result.StatusCode ?? 0);
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
                return Result<FileManagementDto>.Failure(result.Error, result.StatusCode ?? 0);
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
                return Result<FileManagementDto>.Failure(result.Error, result.StatusCode ?? 0);
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
                return Result.Failure(result.Error, result.StatusCode ?? 0);
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
                return Result<ScanResultDto>.Failure(result.Error, result.StatusCode ?? 0);
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
                return Result<ScanResultDto>.Failure(result.Error, result.StatusCode ?? 0);
            }

            var scanResult = MapToScanResultDto(result.Value!);
            return Result<ScanResultDto>.Success(scanResult);
        }
        catch (Exception ex)
        {
            return Result<ScanResultDto>.Failure($"Failed to scan specific folder: {ex.Message}");
        }
    }

    public async Task<Result<BatchJobResponseDto>> OrganizeBatchAsync(
        BatchOrganizeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _httpClient.PostAsync<BatchJobResponse>("/api/v1/file-actions/organize-batch", request, cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<BatchJobResponseDto>.Failure(result.Error, result.StatusCode ?? 0);
            }

            var batchJob = MapToBatchJobResponseDto(result.Value!);
            return Result<BatchJobResponseDto>.Success(batchJob);
        }
        catch (Exception ex)
        {
            return Result<BatchJobResponseDto>.Failure($"Failed to organize batch: {ex.Message}");
        }
    }

    public async Task<Result<BatchJobResponseDto>> GetBatchStatusAsync(
        string jobId,
        bool includeDetails = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queryParam = includeDetails ? "?includeDetails=true" : "";
            var result = await _httpClient.GetAsync<BatchJobResponse>($"/api/v1/file-actions/batch-status/{jobId}{queryParam}", cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<BatchJobResponseDto>.Failure(result.Error, result.StatusCode ?? 0);
            }

            var batchJob = MapToBatchJobResponseDto(result.Value!);
            return Result<BatchJobResponseDto>.Success(batchJob);
        }
        catch (Exception ex)
        {
            return Result<BatchJobResponseDto>.Failure($"Failed to get batch status: {ex.Message}");
        }
    }

    public async Task<Result<IReadOnlyList<string>>> GetDistinctCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _httpClient.GetAsync<string[]>("/api/files/categories", cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<IReadOnlyList<string>>.Failure(result.Error, result.StatusCode ?? 0);
            }

            var categories = result.Value?.ToList() ?? new List<string>();
            return Result<IReadOnlyList<string>>.Success(categories);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<string>>.Failure($"Failed to get distinct categories: {ex.Message}");
        }
    }

    public async Task<Result<object>> IgnoreFileAsync(
        string hash,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                return Result<object>.Failure("Hash cannot be empty");
            }

            var result = await _httpClient.PostAsync<object>($"/api/v1/file-actions/ignore/{hash}", null, cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<object>.Failure(result.Error, result.StatusCode ?? 0);
            }

            return Result<object>.Success(result.Value ?? new object());
        }
        catch (Exception ex)
        {
            return Result<object>.Failure($"Failed to ignore file: {ex.Message}");
        }
    }

    public async Task<Result<MlEvaluationResponse>> QueueMlEvaluationAsync(
        string? filterByCategory = null,
        bool forceReEvaluation = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new MlEvaluationRequest
            {
                FilterByCategory = filterByCategory,
                ForceReEvaluation = forceReEvaluation
            };

            var result = await _httpClient.PostAsync<MlEvaluationResponse>(
                "/api/processing/ml-evaluation/queue",
                request,
                cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<MlEvaluationResponse>.Failure(result.Error, result.StatusCode ?? 0);
            }

            return Result<MlEvaluationResponse>.Success(result.Value!);
        }
        catch (Exception ex)
        {
            return Result<MlEvaluationResponse>.Failure($"Failed to queue ML evaluation: {ex.Message}");
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
            Hash = file.Hash,
            OriginalPath = file.OriginalPath,
            TargetPath = file.TargetPath,
            CreatedDate = file.CreatedAt,
            ClassifiedAt = file.ClassifiedAt,
            Confidence = (decimal)(file.ConfidencePercentage ?? 0),
            Status = !string.IsNullOrWhiteSpace(file.StatusDescription) ? file.StatusDescription : file.Status
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

    /// <summary>
    /// Maps API response to BatchJobResponseDto.
    /// Pure function - deterministic mapping logic.
    /// </summary>
    private static BatchJobResponseDto MapToBatchJobResponseDto(BatchJobResponse batchJob)
    {
        return new BatchJobResponseDto
        {
            JobId = batchJob.JobId,
            Status = batchJob.Status,
            QueuedAt = batchJob.QueuedAt,
            StartedAt = batchJob.StartedAt,
            CompletedAt = batchJob.CompletedAt,
            TotalFiles = batchJob.TotalFiles,
            ProcessedFiles = batchJob.ProcessedFiles,
            SuccessfulFiles = batchJob.SuccessfulFiles,
            FailedFiles = batchJob.FailedFiles,
            Metadata = batchJob.Metadata,
            Errors = batchJob.Errors,
            EstimatedTimeRemaining = batchJob.EstimatedTimeRemaining,
            AverageProcessingTime = batchJob.AverageProcessingTime,
            DetailedResults = batchJob.DetailedResults?.Select(MapToFileProcessingResultDto).ToList()
        };
    }

    /// <summary>
    /// Maps API response to FileProcessingResultDto.
    /// Pure function - deterministic mapping logic.
    /// </summary>
    private static FileProcessingResultDto MapToFileProcessingResultDto(FileProcessingResult result)
    {
        return new FileProcessingResultDto
        {
            FileHash = result.FileHash,
            FileName = result.FileName,
            Success = result.Success,
            TargetPath = result.TargetPath,
            ActualPath = result.ActualPath,
            Error = result.Error,
            ProcessingTime = result.ProcessingTime,
            IsDryRun = result.IsDryRun,
            ProcessedAt = result.ProcessedAt,
            Metadata = result.Metadata
        };
    }
}

// ============================================================================
// API Response Models (for deserialization only - not exposed in public API)
// ============================================================================

/// <summary>
/// API response model for TrackedFile (used for deserialization only).
/// </summary>
internal class TrackedFileResponse
{
    public required string Hash { get; set; }
    public required string FileName { get; set; }
    public required string OriginalPath { get; set; }
    public long FileSize { get; set; }
    public string? FormattedFileSize { get; set; }
    public string Status { get; set; } = string.Empty;
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
    public double? ProcessingDurationMs { get; set; }
}

/// <summary>
/// API response model for ScanResult (used for deserialization only).
/// </summary>
internal class ScanResult
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
/// API response model for BatchJobResponse (used for deserialization only).
/// </summary>
internal class BatchJobResponse
{
    public required string JobId { get; set; }
    public required string Status { get; set; } = "Queued";
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int SuccessfulFiles { get; set; }
    public int FailedFiles { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public TimeSpan? AverageProcessingTime { get; set; }
    public List<FileProcessingResult>? DetailedResults { get; set; }
}

/// <summary>
/// API response model for PaginatedFilesResponse (used for deserialization only).
/// </summary>
internal class PaginatedFilesResponse
{
    public List<TrackedFileResponse> Items { get; set; } = new();
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
}

/// <summary>
/// API response model for FileProcessingResult (used for deserialization only).
/// </summary>
internal class FileProcessingResult
{
    public required string FileHash { get; set; }
    public required string FileName { get; set; }
    public required bool Success { get; set; }
    public required string TargetPath { get; set; }
    public string? ActualPath { get; set; }
    public string? Error { get; set; }
    public TimeSpan? ProcessingTime { get; set; }
    public bool IsDryRun { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object>? Metadata { get; set; }
}
