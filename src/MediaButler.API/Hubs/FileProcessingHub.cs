using Microsoft.AspNetCore.SignalR;

namespace MediaButler.API.Hubs;

/// <summary>
/// SignalR hub specifically for file processing and batch operation notifications.
/// Provides real-time updates for batch file operations, individual file progress, and job status.
/// </summary>
public class FileProcessingHub : Hub<IFileProcessingClient>
{
    /// <summary>
    /// Called when a client connects to the hub.
    /// Logs connection for monitoring and provides connection confirmation.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        var userAgent = Context.GetHttpContext()?.Request.Headers["User-Agent"].ToString() ?? "Unknown";

        Console.WriteLine($"FileProcessingHub: Client connected: {connectionId} from {userAgent}");

        // Send connection confirmation with server time
        await Clients.Caller.ConnectionEstablished(connectionId, DateTime.UtcNow);

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// Performs cleanup and logs disconnection.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;

        if (exception != null)
        {
            Console.WriteLine($"FileProcessingHub: Client disconnected with error: {connectionId} - {exception.Message}");
        }
        else
        {
            Console.WriteLine($"FileProcessingHub: Client disconnected: {connectionId}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Allows clients to join a specific batch job group for targeted notifications.
    /// </summary>
    /// <param name="jobId">The ID of the batch job to monitor</param>
    public async Task JoinBatchJob(string jobId)
    {
        var groupName = $"batch-{jobId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        Console.WriteLine($"Client {Context.ConnectionId} joined batch job group: {jobId}");

        // Send confirmation to the client
        await Clients.Caller.BatchJobJoined(jobId, DateTime.UtcNow);
    }

    /// <summary>
    /// Allows clients to leave a batch job group.
    /// </summary>
    /// <param name="jobId">The ID of the batch job to stop monitoring</param>
    public async Task LeaveBatchJob(string jobId)
    {
        var groupName = $"batch-{jobId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        Console.WriteLine($"Client {Context.ConnectionId} left batch job group: {jobId}");

        // Send confirmation to the client
        await Clients.Caller.BatchJobLeft(jobId, DateTime.UtcNow);
    }

    /// <summary>
    /// Allows clients to join a general file processing group for all file operations.
    /// </summary>
    public async Task JoinFileProcessing()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "file-processing");
        Console.WriteLine($"Client {Context.ConnectionId} joined file processing group");

        await Clients.Caller.FileProcessingJoined(DateTime.UtcNow);
    }

    /// <summary>
    /// Allows clients to leave the general file processing group.
    /// </summary>
    public async Task LeaveFileProcessing()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "file-processing");
        Console.WriteLine($"Client {Context.ConnectionId} left file processing group");

        await Clients.Caller.FileProcessingLeft(DateTime.UtcNow);
    }

    /// <summary>
    /// Ping method for clients to test connection health.
    /// </summary>
    public async Task Ping()
    {
        await Clients.Caller.Pong(DateTime.UtcNow);
    }

    /// <summary>
    /// Allows clients to request the current status of a batch job.
    /// </summary>
    /// <param name="jobId">The ID of the batch job</param>
    public async Task RequestBatchJobStatus(string jobId)
    {
        // This would typically query the job status and send it back
        // For now, we'll just acknowledge the request
        await Clients.Caller.BatchJobStatusRequested(jobId, DateTime.UtcNow);
    }
}

/// <summary>
/// Strongly-typed interface for file processing SignalR hub methods.
/// Provides compile-time safety for batch operation notifications.
/// </summary>
public interface IFileProcessingClient
{
    #region Connection Management

    /// <summary>
    /// Sent when a client successfully connects to the hub.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID</param>
    /// <param name="connectedAt">When the connection was established</param>
    Task ConnectionEstablished(string connectionId, DateTime connectedAt);

    /// <summary>
    /// Sent when a client successfully joins a batch job group.
    /// </summary>
    /// <param name="jobId">The batch job ID</param>
    /// <param name="joinedAt">When the client joined the group</param>
    Task BatchJobJoined(string jobId, DateTime joinedAt);

    /// <summary>
    /// Sent when a client successfully leaves a batch job group.
    /// </summary>
    /// <param name="jobId">The batch job ID</param>
    /// <param name="leftAt">When the client left the group</param>
    Task BatchJobLeft(string jobId, DateTime leftAt);

    /// <summary>
    /// Sent when a client joins the general file processing group.
    /// </summary>
    /// <param name="joinedAt">When the client joined</param>
    Task FileProcessingJoined(DateTime joinedAt);

    /// <summary>
    /// Sent when a client leaves the general file processing group.
    /// </summary>
    /// <param name="leftAt">When the client left</param>
    Task FileProcessingLeft(DateTime leftAt);

    /// <summary>
    /// Response to a ping request for connection health checking.
    /// </summary>
    /// <param name="timestamp">Server timestamp</param>
    Task Pong(DateTime timestamp);

    /// <summary>
    /// Acknowledgment that a batch job status request was received.
    /// </summary>
    /// <param name="jobId">The requested job ID</param>
    /// <param name="requestedAt">When the request was processed</param>
    Task BatchJobStatusRequested(string jobId, DateTime requestedAt);

    #endregion

    #region Batch Job Notifications

    /// <summary>
    /// Notification that a batch job has started processing.
    /// </summary>
    /// <param name="notification">Batch job started notification details</param>
    Task BatchJobStarted(object notification);

    /// <summary>
    /// Notification that a batch job has completed successfully.
    /// </summary>
    /// <param name="notification">Batch job completion details</param>
    Task BatchJobCompleted(object notification);

    /// <summary>
    /// Notification that a batch job has failed with an error.
    /// </summary>
    /// <param name="notification">Batch job failure details</param>
    Task BatchJobFailed(object notification);

    /// <summary>
    /// Notification of batch job progress updates.
    /// </summary>
    /// <param name="jobId">The batch job ID</param>
    /// <param name="totalFiles">Total number of files in the batch</param>
    /// <param name="processedFiles">Number of files processed so far</param>
    /// <param name="successfulFiles">Number of files processed successfully</param>
    /// <param name="failedFiles">Number of files that failed processing</param>
    /// <param name="currentFile">Currently processing file (optional)</param>
    Task BatchJobProgress(string jobId, int totalFiles, int processedFiles,
        int successfulFiles, int failedFiles, string? currentFile);

    #endregion

    #region Individual File Processing

    /// <summary>
    /// Notification that processing has started for an individual file.
    /// </summary>
    /// <param name="notification">File processing started details</param>
    Task FileProcessingStarted(object notification);

    /// <summary>
    /// Notification that processing has completed for an individual file.
    /// </summary>
    /// <param name="notification">File processing completion details</param>
    Task FileProcessingCompleted(object notification);

    /// <summary>
    /// Notification of progress for individual file operations.
    /// </summary>
    /// <param name="fileHash">The file's hash identifier</param>
    /// <param name="fileName">The file name</param>
    /// <param name="operation">Current operation (validate, move, update, etc.)</param>
    /// <param name="progress">Progress percentage (0-100)</param>
    Task FileOperationProgress(string fileHash, string fileName, string operation, int progress);

    #endregion

    #region System Notifications

    /// <summary>
    /// Notification for system-wide file processing status updates.
    /// </summary>
    /// <param name="component">The system component (batch processor, file organizer, etc.)</param>
    /// <param name="status">Current status</param>
    /// <param name="message">Human-readable message</param>
    /// <param name="timestamp">When the status was updated</param>
    Task SystemStatusUpdate(string component, string status, string message, DateTime timestamp);

    /// <summary>
    /// Notification for errors that occur during file processing.
    /// </summary>
    /// <param name="errorType">Type of error (file_access, validation, etc.)</param>
    /// <param name="message">Error message</param>
    /// <param name="fileHash">Hash of the file that caused the error (if applicable)</param>
    /// <param name="jobId">Job ID associated with the error (if applicable)</param>
    /// <param name="details">Additional error details</param>
    Task ProcessingError(string errorType, string message, string? fileHash,
        string? jobId, string? details);

    #endregion
}