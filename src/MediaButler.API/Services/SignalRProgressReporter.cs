using Microsoft.Extensions.Logging;

namespace MediaButler.API.Services;

/// <summary>
/// SignalR-based implementation of IProgressReporter for real-time Web UI updates.
/// Delegates to SignalRNotificationClient for actual notification delivery.
/// Following "Simple Made Easy" - separates progress reporting from job execution.
/// </summary>
public class SignalRProgressReporter : IProgressReporter
{
    private readonly SignalRNotificationClient _signalRClient;
    private readonly ILogger<SignalRProgressReporter> _logger;

    public SignalRProgressReporter(
        SignalRNotificationClient signalRClient,
        ILogger<SignalRProgressReporter> logger)
    {
        _signalRClient = signalRClient ?? throw new ArgumentNullException(nameof(signalRClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ReportJobStartedAsync(string jobId, string jobType, int totalItems)
    {
        try
        {
            await _signalRClient.NotifyBatchJobStartedAsync(jobId, jobType, totalItems);
            _logger.LogDebug("Reported job started: {JobId}, Type: {JobType}, Total: {TotalItems}",
                jobId, jobType, totalItems);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report job started for {JobId}", jobId);
            // Don't throw - progress reporting failures shouldn't break job execution
        }
    }

    public async Task ReportProgressAsync(string jobId, int current, int total, string? currentItem = null)
    {
        try
        {
            await _signalRClient.NotifyBatchJobProgressAsync(jobId, current, total, currentItem ?? string.Empty);
            _logger.LogDebug("Reported progress: {JobId}, {Current}/{Total}, Item: {CurrentItem}",
                jobId, current, total, currentItem ?? "N/A");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report progress for {JobId}", jobId);
            // Don't throw - progress reporting failures shouldn't break job execution
        }
    }

    public async Task ReportJobCompletedAsync(string jobId, string jobType, int totalItems, TimeSpan duration)
    {
        try
        {
            await _signalRClient.NotifyBatchJobCompletedAsync(jobId, jobType, totalItems, duration);
            _logger.LogInformation("Reported job completed: {JobId}, Type: {JobType}, Items: {TotalItems}, Duration: {Duration}ms",
                jobId, jobType, totalItems, duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report job completion for {JobId}", jobId);
            // Don't throw - progress reporting failures shouldn't break job execution
        }
    }

    public async Task ReportJobFailedAsync(string jobId, string jobType, string errorMessage, int itemsProcessed)
    {
        try
        {
            await _signalRClient.NotifyBatchJobFailedAsync(jobId, jobType, errorMessage, itemsProcessed);
            _logger.LogWarning("Reported job failed: {JobId}, Type: {JobType}, Error: {ErrorMessage}, Processed: {ItemsProcessed}",
                jobId, jobType, errorMessage, itemsProcessed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to report job failure for {JobId}", jobId);
            // Don't throw - progress reporting failures shouldn't break job execution
        }
    }
}
