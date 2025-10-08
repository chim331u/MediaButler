using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MediaButler.Batch.Services;

/// <summary>
/// Client for sending SignalR notifications from Batch worker to API.
/// Uses HTTP POST to API endpoint which forwards to SignalR hubs.
/// Implements batching to reduce API calls (10 items or 500ms flush).
/// </summary>
public class SignalRNotificationClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SignalRNotificationClient> _logger;
    private readonly string _notificationEndpoint;
    private readonly int _batchSize;
    private readonly int _flushIntervalMs;

    private readonly ConcurrentQueue<JobNotification> _notificationQueue;
    private readonly Timer _flushTimer;
    private readonly SemaphoreSlim _flushLock;
    private bool _disposed;

    public SignalRNotificationClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SignalRNotificationClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var signalRConfig = configuration.GetSection("SignalRClient");
        _notificationEndpoint = signalRConfig["NotificationEndpoint"] ?? "/api/notifications/batch";
        _batchSize = signalRConfig.GetValue<int>("BatchSize", 10);
        _flushIntervalMs = signalRConfig.GetValue<int>("FlushIntervalMs", 500);

        _notificationQueue = new ConcurrentQueue<JobNotification>();
        _flushLock = new SemaphoreSlim(1, 1);

        // Timer for periodic flush
        _flushTimer = new Timer(async _ => await FlushAsync(), null,
            TimeSpan.FromMilliseconds(_flushIntervalMs),
            TimeSpan.FromMilliseconds(_flushIntervalMs));

        _logger.LogInformation(
            "SignalRNotificationClient initialized: Endpoint={Endpoint}, BatchSize={BatchSize}, FlushInterval={FlushMs}ms",
            _notificationEndpoint, _batchSize, _flushIntervalMs);
    }

    /// <summary>
    /// Notify that a batch job has started.
    /// </summary>
    public async Task NotifyBatchJobStartedAsync(string jobId, string jobType, int totalItems)
    {
        var notification = new JobNotification
        {
            JobId = jobId,
            JobType = jobType,
            EventType = "batch.started",
            Message = $"Batch job started: {jobType}",
            Data = new Dictionary<string, object>
            {
                { "totalItems", totalItems },
                { "startedAt", DateTime.UtcNow }
            }
        };

        await EnqueueNotificationAsync(notification);
    }

    /// <summary>
    /// Notify batch job progress update.
    /// </summary>
    public async Task NotifyBatchJobProgressAsync(string jobId, int processedItems, int totalItems, string currentItem = "")
    {
        var notification = new JobNotification
        {
            JobId = jobId,
            JobType = "batch.progress",
            EventType = "batch.progress",
            Message = $"Processing {processedItems}/{totalItems}",
            Data = new Dictionary<string, object>
            {
                { "processedItems", processedItems },
                { "totalItems", totalItems },
                { "currentItem", currentItem },
                { "percentage", totalItems > 0 ? (int)((double)processedItems / totalItems * 100) : 0 }
            }
        };

        await EnqueueNotificationAsync(notification);
    }

    /// <summary>
    /// Notify that a batch job has completed successfully.
    /// </summary>
    public async Task NotifyBatchJobCompletedAsync(string jobId, string jobType, int totalItems, TimeSpan duration)
    {
        var notification = new JobNotification
        {
            JobId = jobId,
            JobType = jobType,
            EventType = "batch.completed",
            Message = $"Batch job completed: {jobType}",
            Data = new Dictionary<string, object>
            {
                { "totalItems", totalItems },
                { "durationMs", duration.TotalMilliseconds },
                { "completedAt", DateTime.UtcNow }
            }
        };

        await EnqueueNotificationAsync(notification);

        // Force flush on completion
        await FlushAsync();
    }

    /// <summary>
    /// Notify that a batch job has failed.
    /// </summary>
    public async Task NotifyBatchJobFailedAsync(string jobId, string jobType, string errorMessage, int processedItems = 0)
    {
        var notification = new JobNotification
        {
            JobId = jobId,
            JobType = jobType,
            EventType = "batch.failed",
            Message = $"Batch job failed: {errorMessage}",
            Data = new Dictionary<string, object>
            {
                { "errorMessage", errorMessage },
                { "processedItems", processedItems },
                { "failedAt", DateTime.UtcNow }
            }
        };

        await EnqueueNotificationAsync(notification);

        // Force flush on failure
        await FlushAsync();
    }

    private async Task EnqueueNotificationAsync(JobNotification notification)
    {
        _notificationQueue.Enqueue(notification);

        // Flush if batch size reached
        if (_notificationQueue.Count >= _batchSize)
        {
            await FlushAsync();
        }
    }

    private async Task FlushAsync()
    {
        if (_notificationQueue.IsEmpty)
            return;

        // Prevent concurrent flushes
        if (!await _flushLock.WaitAsync(0))
            return;

        try
        {
            var batch = new List<JobNotification>();

            // Dequeue up to batch size
            while (batch.Count < _batchSize && _notificationQueue.TryDequeue(out var notification))
            {
                batch.Add(notification);
            }

            if (batch.Count == 0)
                return;

            // Send batch to API
            var json = JsonSerializer.Serialize(batch);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogDebug("Sending {Count} notifications to {Endpoint}", batch.Count, _notificationEndpoint);

            var response = await _httpClient.PostAsync(_notificationEndpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to send notifications: {StatusCode} - {Reason}",
                    response.StatusCode, response.ReasonPhrase);
            }
            else
            {
                _logger.LogDebug("Successfully sent {Count} notifications", batch.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notifications to API");
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Flush remaining notifications
        FlushAsync().GetAwaiter().GetResult();

        _flushTimer?.Dispose();
        _flushLock?.Dispose();
    }
}

/// <summary>
/// Represents a job notification to be sent to the API.
/// </summary>
public class JobNotification
{
    /// <summary>
    /// Unique job identifier from Hangfire.
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// Type of job (e.g., "batch.file.processing", "model.training").
    /// </summary>
    public string JobType { get; set; } = string.Empty;

    /// <summary>
    /// Event type (e.g., "batch.started", "batch.progress", "batch.completed", "batch.failed").
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Additional data payload for the notification.
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// Timestamp when notification was created.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
