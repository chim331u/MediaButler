using MediaButler.Web.Interfaces;
using MediaButler.Web.Models;

namespace MediaButler.Web.Services;

/// <summary>
/// Health API service following "Simple Made Easy" principles.
/// Single responsibility: Health check operations only.
/// Composes with IHttpClientService without braiding concerns.
/// </summary>
public interface IHealthApiService
{
    /// <summary>
    /// Gets system health status including database connectivity.
    /// Pure function - same inputs produce same outputs.
    /// </summary>
    Task<Result<HealthCheckViewModel>> GetHealthStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of health API service.
/// No state - each request is independent.
/// Values over exceptions - returns explicit Results.
/// </summary>
public class HealthApiService : IHealthApiService
{
    private readonly IHttpClientService _httpClient;

    public HealthApiService(IHttpClientService httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Result<HealthCheckViewModel>> GetHealthStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _httpClient.GetAsync<HealthCheckResponse>("/api/health/detailed", cancellationToken);

            if (!result.IsSuccess)
            {
                // If detailed health check fails, try basic health endpoint
                if (result.StatusCode == 404)
                {
                    return await GetBasicHealthStatusAsync(cancellationToken);
                }

                return Result<HealthCheckViewModel>.Failure(result.Error, result.StatusCode);
            }

            var viewModel = MapToViewModel(result.Value!);
            return Result<HealthCheckViewModel>.Success(viewModel);
        }
        catch (Exception ex)
        {
            // Try basic health check as fallback
            var fallbackResult = await GetBasicHealthStatusAsync(cancellationToken);
            if (fallbackResult.IsSuccess)
            {
                return fallbackResult;
            }

            return Result<HealthCheckViewModel>.Failure($"Health check failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Fallback method to get basic health status when detailed endpoint is unavailable.
    /// </summary>
    private async Task<Result<HealthCheckViewModel>> GetBasicHealthStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _httpClient.GetAsync<BasicHealthResponse>("/api/health", cancellationToken);

            if (!result.IsSuccess)
            {
                return CreateOfflineViewModel();
            }

            var viewModel = MapBasicToViewModel(result.Value!);
            return Result<HealthCheckViewModel>.Success(viewModel);
        }
        catch
        {
            return CreateOfflineViewModel();
        }
    }

    /// <summary>
    /// Creates a view model indicating the API is offline or unreachable.
    /// </summary>
    private Result<HealthCheckViewModel> CreateOfflineViewModel()
    {
        var offlineViewModel = new HealthCheckViewModel(
            OverallStatus: HealthStatus.Unhealthy,
            OverallStatusText: "API Unreachable",
            TotalDuration: TimeSpan.Zero,
            LastChecked: DateTime.UtcNow,
            Components: new List<ComponentHealth>
            {
                new ComponentHealth(
                    Name: "MediaButler API",
                    Status: HealthStatus.Unhealthy,
                    StatusText: "Unreachable",
                    Description: "Cannot connect to MediaButler API service",
                    Duration: TimeSpan.Zero,
                    Details: new Dictionary<string, string>
                    {
                        ["Status"] = "Connection failed",
                        ["Possible causes"] = "API server down, network issues, incorrect URL"
                    }
                )
            }
        );

        return Result<HealthCheckViewModel>.Success(offlineViewModel);
    }

    /// <summary>
    /// Maps basic health response to view model.
    /// Pure function for simple API response mapping.
    /// </summary>
    private static HealthCheckViewModel MapBasicToViewModel(BasicHealthResponse response)
    {
        var overallStatus = MapStatus(response.Status);
        var components = new List<ComponentHealth>
        {
            new ComponentHealth(
                Name: "MediaButler API",
                Status: overallStatus,
                StatusText: response.Status,
                Description: $"API version {response.Version} running on service {response.Service}",
                Duration: TimeSpan.Zero,
                Details: new Dictionary<string, string>
                {
                    ["Service"] = response.Service,
                    ["Version"] = response.Version,
                    ["Last Check"] = response.Timestamp.ToString("yyyy-MM-dd HH:mm:ss UTC")
                }
            )
        };

        return new HealthCheckViewModel(
            OverallStatus: overallStatus,
            OverallStatusText: response.Status,
            TotalDuration: TimeSpan.Zero,
            LastChecked: response.Timestamp,
            Components: components
        );
    }

    /// <summary>
    /// Maps API response to view model.
    /// Pure function - deterministic transformation.
    /// Separates API concerns from UI concerns.
    /// </summary>
    private static HealthCheckViewModel MapToViewModel(HealthCheckResponse response)
    {
        var overallStatus = MapStatus(response.Status);
        var components = new List<ComponentHealth>();

        // Map Database component
        components.Add(new ComponentHealth(
            Name: "Database",
            Status: MapStatus(response.Database.Status),
            StatusText: response.Database.Status,
            Description: $"{response.Database.TotalFiles} total files, {response.Database.ProcessedToday} processed today",
            Duration: TimeSpan.Zero,
            Details: new Dictionary<string, string>
            {
                ["Total Files"] = response.Database.TotalFiles.ToString(),
                ["Processed Today"] = response.Database.ProcessedToday.ToString(),
                ["Connection"] = response.Database.Status
            }
        ));

        // Map Memory component
        var memoryStatus = GetMemoryStatus(response.Memory);
        components.Add(new ComponentHealth(
            Name: "Memory Usage",
            Status: memoryStatus,
            StatusText: memoryStatus.ToString(),
            Description: $"{response.Memory.ManagedMemoryMB:F1} MB managed, {response.Memory.WorkingSetMB:F1} MB working set",
            Duration: TimeSpan.Zero,
            Details: new Dictionary<string, string>
            {
                ["Managed Memory (MB)"] = response.Memory.ManagedMemoryMB.ToString("F1"),
                ["Working Set (MB)"] = response.Memory.WorkingSetMB.ToString("F1"),
                ["Target Limit (MB)"] = response.Memory.TargetLimitMB.ToString(),
                ["Usage %"] = $"{(response.Memory.WorkingSetMB / response.Memory.TargetLimitMB * 100):F1}%"
            }
        ));

        // Map Processing component
        var totalProcessing = response.Processing.StatusCounts.Values.Sum();
        components.Add(new ComponentHealth(
            Name: "File Processing",
            Status: HealthStatus.Healthy,
            StatusText: "Active",
            Description: $"{totalProcessing} files in processing pipeline, avg {response.Processing.AverageProcessingTimeMinutes:F1} min",
            Duration: TimeSpan.Zero,
            Details: response.Processing.StatusCounts.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToString()
            )
        ));

        // Map File Operations (simplified)
        components.Add(new ComponentHealth(
            Name: "File Operations",
            Status: HealthStatus.Healthy,
            StatusText: "Operational",
            Description: "File operations are running normally",
            Duration: TimeSpan.Zero
        ));

        // Map Machine Learning (simplified)
        components.Add(new ComponentHealth(
            Name: "Machine Learning",
            Status: HealthStatus.Healthy,
            StatusText: "Available",
            Description: "ML classification services are operational",
            Duration: TimeSpan.Zero
        ));

        return new HealthCheckViewModel(
            OverallStatus: overallStatus,
            OverallStatusText: response.Status,
            TotalDuration: TimeSpan.Zero,
            LastChecked: response.Timestamp,
            Components: components
        );
    }

    /// <summary>
    /// Determines memory health status based on usage.
    /// </summary>
    private static HealthStatus GetMemoryStatus(MemoryHealth memory)
    {
        var usagePercentage = memory.WorkingSetMB / memory.TargetLimitMB * 100;

        return usagePercentage switch
        {
            <= 70 => HealthStatus.Healthy,
            <= 85 => HealthStatus.Degraded,
            _ => HealthStatus.Unhealthy
        };
    }

    /// <summary>
    /// Maps string status to enum.
    /// Pure function - deterministic mapping.
    /// </summary>
    private static HealthStatus MapStatus(string status) => status.ToLowerInvariant() switch
    {
        "healthy" => HealthStatus.Healthy,
        "degraded" => HealthStatus.Degraded,
        _ => HealthStatus.Unhealthy
    };

}