using Microsoft.AspNetCore.SignalR;
using MediaButler.Core.Enums;

namespace MediaButler.API.Hubs;

/// <summary>
/// SignalR hub for real-time notifications in MediaButler application.
/// Provides real-time updates for file processing, moves, and system status.
/// Follows "Simple Made Easy" principles with clear, focused responsibilities.
/// </summary>
public class NotificationHub : Hub<INotificationClient>
{
    /// <summary>
    /// Called when a client connects to the hub.
    /// Logs connection for monitoring and debugging.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var connectionId = Context.ConnectionId;
        var userAgent = Context.GetHttpContext()?.Request.Headers["User-Agent"].ToString() ?? "Unknown";

        // Log connection for debugging in development
        Console.WriteLine($"SignalR Client connected: {connectionId} from {userAgent}");

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// Logs disconnection for monitoring and debugging.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;

        if (exception != null)
        {
            Console.WriteLine($"SignalR Client disconnected with error: {connectionId} - {exception.Message}");
        }
        else
        {
            Console.WriteLine($"SignalR Client disconnected: {connectionId}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Allows clients to join specific groups for targeted notifications.
    /// Useful for filtering notifications by file type, status, or user preferences.
    /// </summary>
    /// <param name="groupName">The name of the group to join</param>
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        Console.WriteLine($"Client {Context.ConnectionId} joined group: {groupName}");
    }

    /// <summary>
    /// Allows clients to leave specific groups.
    /// </summary>
    /// <param name="groupName">The name of the group to leave</param>
    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        Console.WriteLine($"Client {Context.ConnectionId} left group: {groupName}");
    }
}

/// <summary>
/// Strongly-typed interface for SignalR hub methods.
/// Provides compile-time safety for notification method calls.
/// </summary>
public interface INotificationClient
{
    /// <summary>
    /// Notification for file movement operations.
    /// </summary>
    /// <param name="fileId">The ID of the file being moved</param>
    /// <param name="fileName">The name of the file</param>
    /// <param name="status">The current status of the move operation</param>
    Task MoveFileNotification(int fileId, string fileName, string status);

    /// <summary>
    /// Notification for background job progress.
    /// </summary>
    /// <param name="jobType">The type of job (scan, train, classify, etc.)</param>
    /// <param name="message">Human-readable status message</param>
    /// <param name="progress">Progress percentage (0-100)</param>
    Task JobProgressNotification(string jobType, string message, int progress);

    /// <summary>
    /// Notification for file classification results.
    /// </summary>
    /// <param name="fileId">The ID of the classified file</param>
    /// <param name="fileName">The name of the file</param>
    /// <param name="suggestedCategory">The ML-suggested category</param>
    /// <param name="confidence">The confidence score (0-1)</param>
    Task ClassificationNotification(int fileId, string fileName, string suggestedCategory, decimal confidence);

    /// <summary>
    /// Notification for system errors that require user attention.
    /// </summary>
    /// <param name="errorType">The type of error (file_access, ml_model, etc.)</param>
    /// <param name="message">Human-readable error message</param>
    /// <param name="details">Additional technical details</param>
    Task ErrorNotification(string errorType, string message, string? details);

    /// <summary>
    /// Notification for general system status updates.
    /// </summary>
    /// <param name="component">The system component (scanner, classifier, etc.)</param>
    /// <param name="status">The current status</param>
    /// <param name="message">Human-readable status message</param>
    Task SystemStatusNotification(string component, string status, string message);

    /// <summary>
    /// Notification for new file discovery events.
    /// Signals the web UI to refresh the files list.
    /// </summary>
    /// <param name="fileName">The name of the discovered file</param>
    /// <param name="filePath">The path of the discovered file</param>
    /// <param name="discoveredAt">When the file was discovered</param>
    Task FileDiscoveryNotification(string fileName, string filePath, DateTime discoveredAt);
}