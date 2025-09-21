using Microsoft.AspNetCore.SignalR;
using MediaButler.API.Hubs;
using MediaButler.Core.Enums;

namespace MediaButler.API.Services;

/// <summary>
/// Service for sending real-time notifications via SignalR.
/// Provides a simple interface for background services and controllers
/// to send notifications to connected clients.
/// Follows "Simple Made Easy" principles with clear, focused methods.
/// </summary>
public interface ISignalRNotificationService
{
    /// <summary>
    /// Send a file movement notification to all connected clients.
    /// </summary>
    Task NotifyFileMoveAsync(int fileId, string fileName, string status);

    /// <summary>
    /// Send a background job progress notification.
    /// </summary>
    Task NotifyJobProgressAsync(string jobType, string message, int progress = 0);

    /// <summary>
    /// Send a file classification notification.
    /// </summary>
    Task NotifyFileClassificationAsync(int fileId, string fileName, string suggestedCategory, decimal confidence);

    /// <summary>
    /// Send an error notification for issues requiring user attention.
    /// </summary>
    Task NotifyErrorAsync(string errorType, string message, string? details = null);

    /// <summary>
    /// Send a system status notification.
    /// </summary>
    Task NotifySystemStatusAsync(string component, string status, string message);

    /// <summary>
    /// Send a file discovery notification to trigger UI refresh.
    /// </summary>
    Task NotifyFileDiscoveryAsync(string fileName, string filePath, DateTime discoveredAt);
}

/// <summary>
/// Implementation of SignalR notification service.
/// Handles the actual transmission of notifications to connected clients.
/// </summary>
public class SignalRNotificationService : ISignalRNotificationService
{
    private readonly IHubContext<NotificationHub, INotificationClient> _hubContext;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(
        IHubContext<NotificationHub, INotificationClient> hubContext,
        ILogger<SignalRNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task NotifyFileMoveAsync(int fileId, string fileName, string status)
    {
        try
        {
            await _hubContext.Clients.All.MoveFileNotification(fileId, fileName, status);
            _logger.LogDebug("Sent file move notification: {FileName} - {Status}", fileName, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send file move notification for {FileName}", fileName);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyJobProgressAsync(string jobType, string message, int progress = 0)
    {
        try
        {
            await _hubContext.Clients.All.JobProgressNotification(jobType, message, progress);
            _logger.LogDebug("Sent job progress notification: {JobType} - {Message} ({Progress}%)",
                jobType, message, progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send job progress notification for {JobType}", jobType);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyFileClassificationAsync(int fileId, string fileName, string suggestedCategory, decimal confidence)
    {
        try
        {
            await _hubContext.Clients.All.ClassificationNotification(fileId, fileName, suggestedCategory, confidence);
            _logger.LogDebug("Sent classification notification: {FileName} -> {Category} ({Confidence:P})",
                fileName, suggestedCategory, confidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send classification notification for {FileName}", fileName);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyErrorAsync(string errorType, string message, string? details = null)
    {
        try
        {
            await _hubContext.Clients.All.ErrorNotification(errorType, message, details);
            _logger.LogDebug("Sent error notification: {ErrorType} - {Message}", errorType, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send error notification: {ErrorType}", errorType);
        }
    }

    /// <inheritdoc/>
    public async Task NotifySystemStatusAsync(string component, string status, string message)
    {
        try
        {
            await _hubContext.Clients.All.SystemStatusNotification(component, status, message);
            _logger.LogDebug("Sent system status notification: {Component} - {Status}", component, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send system status notification for {Component}", component);
        }
    }

    /// <inheritdoc/>
    public async Task NotifyFileDiscoveryAsync(string fileName, string filePath, DateTime discoveredAt)
    {
        try
        {
            await _hubContext.Clients.All.FileDiscoveryNotification(fileName, filePath, discoveredAt);
            _logger.LogDebug("Sent file discovery notification: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send file discovery notification for {FileName}", fileName);
        }
    }
}