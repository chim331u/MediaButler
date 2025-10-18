using Hangfire;
using MediaButler.Services.Background;
using Microsoft.Extensions.Logging;

namespace MediaButler.API.Jobs.Recurring;

/// <summary>
/// Hangfire recurring job for file discovery.
/// Replaces FileSystemWatcher with reliable polling-based approach.
/// Runs every configured interval to scan watch folders for new files.
/// </summary>
[Queue("default")]
[AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 10, 30 })]
public class FileDiscoveryJob
{
    private readonly IFileDiscoveryService _fileDiscoveryService;
    private readonly ILogger<FileDiscoveryJob> _logger;

    public FileDiscoveryJob(
        IFileDiscoveryService fileDiscoveryService,
        ILogger<FileDiscoveryJob> logger)
    {
        _fileDiscoveryService = fileDiscoveryService ?? throw new ArgumentNullException(nameof(fileDiscoveryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Scans configured watch folders for new files.
    /// Called by Hangfire on a recurring schedule.
    /// </summary>
    [JobDisplayName("File Discovery Scan")]
    public async Task ScanForNewFilesAsync()
    {
        _logger.LogInformation("File discovery job started");

        try
        {
            var result = await _fileDiscoveryService.ScanFoldersAsync(CancellationToken.None);

            if (result.IsSuccess)
            {
                _logger.LogInformation("File discovery job completed successfully. Discovered {FileCount} files", result.Value);
            }
            else
            {
                _logger.LogWarning("File discovery job completed with errors: {Error}", result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File discovery job failed with exception");
            throw; // Re-throw to let Hangfire handle retry logic
        }
    }
}
