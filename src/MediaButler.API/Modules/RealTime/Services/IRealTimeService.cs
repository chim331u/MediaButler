using MediaButler.API.Modules.RealTime.Models;

namespace MediaButler.API.Modules.RealTime.Services;

/// <summary>
/// Unified service for all real-time communications.
/// Abstracts SignalR Hubs and notification logic from the rest of the application.
/// </summary>
public interface IRealTimeService
{
    // --- File Operations ---
    Task NotifyFileMoveAsync(int fileId, string fileName, string status);
    Task NotifyFileDiscoveryAsync(string fileName, string filePath, DateTime discoveredAt);
    Task NotifyFileClassificationAsync(int fileId, string fileName, string suggestedCategory, decimal confidence);

    // --- System & Jobs ---
    Task NotifyJobProgressAsync(string jobType, string message, int progress = 0);
    Task NotifySystemStatusAsync(string component, string status, string message);
    Task NotifyErrorAsync(string errorType, string message, string? details = null);
    
    /// <summary>
    /// Dispatches a generic batch notification received from background workers.
    /// Handles routing to appropriate hubs and events based on the notification type.
    /// </summary>
    Task DispatchBatchNotificationAsync(BatchNotification notification);
}
