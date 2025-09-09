using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services.Background;

/// <summary>
/// Hosted service wrapper for the FileDiscoveryService to integrate with .NET hosting model.
/// Manages the lifecycle of file discovery operations following "Simple Made Easy" principles.
/// </summary>
public class FileDiscoveryHostedService : IHostedService
{
    private readonly IFileDiscoveryService _fileDiscoveryService;
    private readonly ILogger<FileDiscoveryHostedService> _logger;

    public FileDiscoveryHostedService(
        IFileDiscoveryService fileDiscoveryService,
        ILogger<FileDiscoveryHostedService> logger)
    {
        _fileDiscoveryService = fileDiscoveryService ?? throw new ArgumentNullException(nameof(fileDiscoveryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to discovery events for logging
        _fileDiscoveryService.FileDiscovered += OnFileDiscovered;
        _fileDiscoveryService.DiscoveryError += OnDiscoveryError;
    }

    /// <summary>
    /// Starts the file discovery service when the host starts.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting File Discovery Hosted Service");

        try
        {
            var result = await _fileDiscoveryService.StartMonitoringAsync(cancellationToken);
            
            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "File Discovery Hosted Service started successfully. Monitoring {FolderCount} folders",
                    _fileDiscoveryService.MonitoredPaths.Count());
            }
            else
            {
                _logger.LogError("Failed to start file discovery monitoring: {Error}", result.Error);
                throw new InvalidOperationException($"Failed to start file discovery: {result.Error}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting File Discovery Hosted Service");
            throw;
        }
    }

    /// <summary>
    /// Stops the file discovery service when the host stops.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping File Discovery Hosted Service");

        try
        {
            var result = await _fileDiscoveryService.StopMonitoringAsync(cancellationToken);
            
            if (result.IsSuccess)
            {
                _logger.LogInformation("File Discovery Hosted Service stopped successfully");
            }
            else
            {
                _logger.LogWarning("File discovery service stop reported an error: {Error}", result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping File Discovery Hosted Service");
            // Don't rethrow during shutdown to avoid preventing graceful shutdown
        }
        finally
        {
            // Unsubscribe from events
            _fileDiscoveryService.FileDiscovered -= OnFileDiscovered;
            _fileDiscoveryService.DiscoveryError -= OnDiscoveryError;
        }
    }

    /// <summary>
    /// Handles file discovered events for logging and monitoring.
    /// </summary>
    private void OnFileDiscovered(object? sender, FileDiscoveredEventArgs e)
    {
        _logger.LogInformation("File discovered: {FilePath} at {DiscoveryTime}", 
            e.FilePath, e.DiscoveredAt);
    }

    /// <summary>
    /// Handles discovery error events for logging and monitoring.
    /// </summary>
    private void OnDiscoveryError(object? sender, FileDiscoveryErrorEventArgs e)
    {
        if (e.Exception != null)
        {
            _logger.LogError(e.Exception, 
                "File discovery error for {FilePath}: {ErrorMessage}", 
                e.FilePath ?? "<unknown>", e.ErrorMessage);
        }
        else
        {
            _logger.LogError(
                "File discovery error for {FilePath}: {ErrorMessage}", 
                e.FilePath ?? "<unknown>", e.ErrorMessage);
        }
    }
}