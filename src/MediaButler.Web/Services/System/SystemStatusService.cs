using MediaButler.Web.Models;
using System.Net.Http.Json;
using System.Diagnostics;

namespace MediaButler.Web.Services.System;

/// <summary>
/// Service implementation for retrieving system status information from the API
/// </summary>
public class SystemStatusService : ISystemStatusService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SystemStatusService> _logger;
    private readonly Stopwatch _uptimeStopwatch;

    public SystemStatusService(HttpClient httpClient, ILogger<SystemStatusService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _uptimeStopwatch = Stopwatch.StartNew();
    }

    public async Task<SystemStatusModel> GetSystemStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching comprehensive system status");

            // Fetch data from multiple API endpoints in parallel
            var tasks = new Task[]
            {
                GetFileStatisticsAsync(cancellationToken),
                GetHealthStatusAsync(cancellationToken),
                GetProcessingQueueStatusAsync(cancellationToken),
                GetStorageStatusAsync(cancellationToken)
            };

            await Task.WhenAll(tasks);

            var fileStats = await (Task<FileStatistics>)tasks[0];
            var healthStatus = await (Task<HealthStatus>)tasks[1];
            var queueStatus = await (Task<ProcessingQueueStatus>)tasks[2];
            var storageStatus = await (Task<StorageStatus>)tasks[3];

            var systemStatus = new SystemStatusModel
            {
                // File Statistics
                TotalFiles = fileStats.TotalFiles,
                PendingFiles = fileStats.PendingFiles,
                ProcessedFiles = fileStats.ProcessedFiles,
                ErrorFiles = fileStats.ErrorFiles,

                // System Health
                IsApiHealthy = healthStatus.IsHealthy,
                MemoryUsageMB = healthStatus.MemoryUsageMB,
                UptimeSpan = _uptimeStopwatch.Elapsed,

                // Processing Queue
                QueueSize = queueStatus.QueueSize,
                ActiveJobs = queueStatus.ActiveJobs,
                CompletedToday = queueStatus.CompletedToday,
                AvgProcessingTimeMs = queueStatus.AvgProcessingTimeMs,

                // Storage
                StorageUsedGB = storageStatus.UsedGB,
                StorageTotalGB = storageStatus.TotalGB,
                FilesTotalSizeGB = storageStatus.FilesTotalSizeGB,

                Timestamp = DateTime.UtcNow
            };

            _logger.LogDebug("System status retrieved successfully");
            return systemStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system status");
            
            // Return a basic status with error indication
            return new SystemStatusModel
            {
                IsApiHealthy = false,
                UptimeSpan = _uptimeStopwatch.Elapsed,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    public async Task<bool> IsSystemHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking system health");
            return false;
        }
    }

    public async Task<ProcessingQueueStatus> GetProcessingQueueStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<ProcessingQueueStatus>("/api/processing/queue/status", cancellationToken);
            return response ?? new ProcessingQueueStatus();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving processing queue status");
            return new ProcessingQueueStatus();
        }
    }

    // Private helper methods for fetching different status components
    private async Task<FileStatistics> GetFileStatisticsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<FileStatistics>("/api/files/statistics", cancellationToken);
            return response ?? new FileStatistics();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving file statistics");
            return new FileStatistics();
        }
    }

    private async Task<HealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var healthCheck = await _httpClient.GetAsync("/api/health", cancellationToken);
            var memoryInfo = await GetMemoryUsageAsync(cancellationToken);

            return new HealthStatus
            {
                IsHealthy = healthCheck.IsSuccessStatusCode,
                MemoryUsageMB = memoryInfo
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving health status");
            return new HealthStatus { IsHealthy = false };
        }
    }

    private async Task<StorageStatus> GetStorageStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<StorageStatus>("/api/system/storage", cancellationToken);
            return response ?? new StorageStatus();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving storage status");
            return new StorageStatus();
        }
    }

    private async Task<int?> GetMemoryUsageAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<MemoryInfo>("/api/system/memory", cancellationToken);
            return response?.UsageMB;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving memory usage");
            return null;
        }
    }

    // Helper data models for API responses
    private record FileStatistics
    {
        public int TotalFiles { get; init; }
        public int PendingFiles { get; init; }
        public int ProcessedFiles { get; init; }
        public int ErrorFiles { get; init; }
    }

    private record HealthStatus
    {
        public bool IsHealthy { get; init; }
        public int? MemoryUsageMB { get; init; }
    }

    private record StorageStatus
    {
        public double UsedGB { get; init; }
        public double TotalGB { get; init; }
        public double FilesTotalSizeGB { get; init; }
    }

    private record MemoryInfo
    {
        public int UsageMB { get; init; }
    }
}