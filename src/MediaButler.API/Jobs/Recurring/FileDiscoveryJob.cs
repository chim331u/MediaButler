using Hangfire;
using MediaButler.Core.Enums;
using MediaButler.Services.Background;
using MediaButler.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace MediaButler.API.Jobs.Recurring;

/// <summary>
/// Hangfire recurring job for file discovery and ML classification triggering.
/// Replaces FileSystemWatcher with reliable polling-based approach.
/// Runs every configured interval to scan watch folders for new files,
/// then automatically enqueues discovered files for ML classification.
/// </summary>
[Queue("default")]
[AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 10, 30 })]
public class FileDiscoveryJob
{
    private readonly IFileDiscoveryService _fileDiscoveryService;
    private readonly IFileProcessingQueue _fileProcessingQueue;
    private readonly IFileService _fileService;
    private readonly ILogger<FileDiscoveryJob> _logger;

    public FileDiscoveryJob(
        IFileDiscoveryService fileDiscoveryService,
        IFileProcessingQueue fileProcessingQueue,
        IFileService fileService,
        ILogger<FileDiscoveryJob> logger)
    {
        _fileDiscoveryService = fileDiscoveryService ?? throw new ArgumentNullException(nameof(fileDiscoveryService));
        _fileProcessingQueue = fileProcessingQueue ?? throw new ArgumentNullException(nameof(fileProcessingQueue));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Scans configured watch folders for new files and triggers ML classification.
    /// Called by Hangfire on a recurring schedule.
    /// </summary>
    [JobDisplayName("File Discovery & Classification Trigger")]
    public async Task ScanForNewFilesAsync()
    {
        _logger.LogInformation("File discovery job started");

        try
        {
            // Step 1: Scan folders for new files
            var result = await _fileDiscoveryService.ScanFoldersAsync(CancellationToken.None);

            if (result.IsSuccess)
            {
                var discoveredCount = result.Value;
                _logger.LogInformation("File discovery completed successfully. Discovered {FileCount} new files", discoveredCount);

                // Step 2: Trigger ML classification for newly discovered files
                if (discoveredCount > 0)
                {
                    await TriggerMLClassificationAsync();
                }
                else
                {
                    _logger.LogInformation("No new files to classify");
                }
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

    /// <summary>
    /// Triggers ML classification for all files in NEW status by enqueuing them
    /// for processing by the FileProcessingService background worker.
    /// </summary>
    private async Task TriggerMLClassificationAsync()
    {
        try
        {
            _logger.LogInformation("Triggering ML classification for newly discovered files");

            // Get all files in NEW status (just discovered, not yet classified)
            var newFilesResult = await _fileService.GetFilesByStatusAsync(FileStatus.New, CancellationToken.None);

            if (!newFilesResult.IsSuccess)
            {
                _logger.LogWarning("Failed to retrieve files for classification: {Error}", newFilesResult.Error);
                return;
            }

            var newFiles = newFilesResult.Value.ToList();

            if (newFiles.Count == 0)
            {
                _logger.LogInformation("No files in NEW status to classify");
                return;
            }

            _logger.LogInformation("Enqueuing {FileCount} files for ML classification", newFiles.Count);

            // Enqueue each file for ML classification processing
            var enqueuedCount = 0;
            foreach (var file in newFiles)
            {
                try
                {
                    await _fileProcessingQueue.EnqueueAsync(file, CancellationToken.None);
                    enqueuedCount++;

                    _logger.LogDebug(
                        "Enqueued file {FileHash} ({FileName}) for ML classification",
                        file.Hash, file.FileName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to enqueue file {FileHash} ({FileName}) for classification",
                        file.Hash, file.FileName);
                }
            }

            _logger.LogInformation(
                "ML classification triggered: {EnqueuedCount}/{TotalCount} files enqueued successfully. " +
                "FileProcessingService will process them asynchronously.",
                enqueuedCount, newFiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering ML classification");
            // Don't throw - this is a secondary operation that shouldn't fail the entire job
        }
    }
}
