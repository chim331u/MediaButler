using MediaButler.Core.Enums;
using MediaButler.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services.Background;

/// <summary>
/// Hosted service that coordinates between the file processing queue and the processing coordinator.
/// Manages batch processing workflow and progress reporting following "Simple Made Easy" principles.
/// </summary>
public class ProcessingCoordinatorHostedService : BackgroundService
{
    private readonly IFileProcessingQueue _processingQueue;
    private readonly IProcessingCoordinator _coordinator;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ProcessingCoordinatorHostedService> _logger;
    
    // ARM32 optimized configuration
    private const int BatchProcessingIntervalMs = 5000; // Process batches every 5 seconds
    private const int MaxBatchSize = 10; // Small batches for ARM32
    private const int MaxHighPriorityBatchSize = 5; // Even smaller high priority batches
    
    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromMilliseconds(100);

    public ProcessingCoordinatorHostedService(
        IFileProcessingQueue processingQueue,
        IProcessingCoordinator coordinator,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ProcessingCoordinatorHostedService> logger)
    {
        _processingQueue = processingQueue ?? throw new ArgumentNullException(nameof(processingQueue));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to coordinator events for logging
        _coordinator.BatchProcessingStarted += OnBatchProcessingStarted;
        _coordinator.BatchProcessingCompleted += OnBatchProcessingCompleted;
        _coordinator.ProcessingProgress += OnProcessingProgress;
    }

    /// <summary>
    /// Main execution loop that coordinates batch processing from the queue.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Processing Coordinator Hosted Service started");

        try
        {
            // Start the coordinator
            var startResult = await _coordinator.StartAsync(stoppingToken);
            if (!startResult.IsSuccess)
            {
                _logger.LogError("Failed to start Processing Coordinator: {Error}", startResult.Error);
                return;
            }

            // Main coordination loop
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Process high priority files first
                    await ProcessHighPriorityBatch(stoppingToken);
                    
                    // Process normal priority files
                    await ProcessNormalPriorityBatch(stoppingToken);
                    
                    // Wait before next processing cycle
                    await Task.Delay(BatchProcessingIntervalMs, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Processing coordinator cancellation requested");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in processing coordinator main loop");
                    
                    // Brief delay before retrying to prevent error loops
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
        finally
        {
            // Stop the coordinator
            var stopResult = await _coordinator.StopAsync(stoppingToken);
            if (!stopResult.IsSuccess)
            {
                _logger.LogWarning("Processing Coordinator stop reported an error: {Error}", stopResult.Error);
            }

            _logger.LogInformation("Processing Coordinator Hosted Service stopped");
        }
    }

    /// <summary>
    /// Processes a batch of high priority files from the queue.
    /// </summary>
    private async Task ProcessHighPriorityBatch(CancellationToken cancellationToken)
    {
        var highPriorityFiles = new List<Core.Entities.TrackedFile>();
        
        // Collect high priority files from queue
        for (int i = 0; i < MaxHighPriorityBatchSize; i++)
        {
            var file = await _processingQueue.DequeueAsync(cancellationToken);
            if (file == null) break;
            
            // Check if this is actually a high priority file by looking for files awaiting classification
            if (await ShouldProcessFile(file, isHighPriority: true))
            {
                highPriorityFiles.Add(file);
            }
            else
            {
                // Put it back in queue if it doesn't need immediate processing
                await _processingQueue.EnqueueAsync(file, cancellationToken);
            }
        }

        if (highPriorityFiles.Any())
        {
            _logger.LogDebug("Processing high priority batch: {FileCount} files", highPriorityFiles.Count);
            
            var result = await _coordinator.ProcessHighPriorityBatchAsync(highPriorityFiles, cancellationToken);
            
            if (!result.IsSuccess)
            {
                _logger.LogWarning("High priority batch processing failed: {Error}", result.Error);
                
                // Re-queue files for retry
                foreach (var file in highPriorityFiles)
                {
                    await _processingQueue.EnqueueHighPriorityAsync(file, cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Processes a batch of normal priority files from the queue.
    /// </summary>
    private async Task ProcessNormalPriorityBatch(CancellationToken cancellationToken)
    {
        var normalFiles = new List<Core.Entities.TrackedFile>();
        
        // Collect normal priority files from queue
        for (int i = 0; i < MaxBatchSize; i++)
        {
            var file = await _processingQueue.DequeueAsync(cancellationToken);
            if (file == null) break;
            
            if (await ShouldProcessFile(file, isHighPriority: false))
            {
                normalFiles.Add(file);
            }
        }

        if (normalFiles.Any())
        {
            _logger.LogDebug("Processing normal priority batch: {FileCount} files", normalFiles.Count);
            
            var result = await _coordinator.ProcessBatchAsync(normalFiles, cancellationToken);
            
            if (!result.IsSuccess)
            {
                _logger.LogWarning("Normal priority batch processing failed: {Error}", result.Error);
                
                // Re-queue files for retry
                foreach (var file in normalFiles)
                {
                    await _processingQueue.EnqueueAsync(file, cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Determines if a file should be processed based on its current status.
    /// </summary>
    private async Task<bool> ShouldProcessFile(Core.Entities.TrackedFile file, bool isHighPriority)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
            
            // Get current file status from database
            var currentFileResult = await fileService.GetFileByHashAsync(file.Hash);
            
            if (!currentFileResult.IsSuccess)
            {
                _logger.LogWarning("Could not get current status for file {Hash}: {Error}", 
                    file.Hash, currentFileResult.Error);
                return false;
            }

            var currentFile = currentFileResult.Value;
            
            // Process files that are new or ready for classification
            var shouldProcess = currentFile.Status == FileStatus.New || 
                               currentFile.Status == FileStatus.Processing ||
                               (currentFile.Status == FileStatus.Retry && isHighPriority);

            if (!shouldProcess)
            {
                _logger.LogDebug("Skipping file {Hash} with status {Status}", file.Hash, currentFile.Status);
            }

            return shouldProcess;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking file {Hash} processing status", file.Hash);
            return false;
        }
    }

    /// <summary>
    /// Handles batch processing started events for logging and monitoring.
    /// </summary>
    private void OnBatchProcessingStarted(object? sender, BatchProcessingStartedEventArgs e)
    {
        _logger.LogInformation(
            "Batch processing started: {BatchSize} files, Priority: {Priority}",
            e.BatchSize, e.IsHighPriority ? "HIGH" : "NORMAL");
    }

    /// <summary>
    /// Handles batch processing completed events for logging and monitoring.
    /// </summary>
    private void OnBatchProcessingCompleted(object? sender, BatchProcessingCompletedEventArgs e)
    {
        var result = e.Result;
        _logger.LogInformation(
            "Batch processing completed: {Processed}/{Total} files, Success: {SuccessCount}, Failed: {FailedCount}, Duration: {Duration}ms, Priority: {Priority}",
            result.ProcessedFiles, result.TotalFiles, result.SuccessfulClassifications, 
            result.FailedClassifications, result.ProcessingDuration.TotalMilliseconds,
            e.IsHighPriority ? "HIGH" : "NORMAL");

        if (result.Errors.Any())
        {
            _logger.LogWarning("Batch processing had {ErrorCount} errors: {FirstError}",
                result.Errors.Count, result.Errors.First());
        }
    }

    /// <summary>
    /// Handles processing progress events for logging and monitoring.
    /// </summary>
    private void OnProcessingProgress(object? sender, ProcessingProgressEventArgs e)
    {
        if (e.TotalFiles > 0 && (e.ProcessedFiles % 5 == 0 || e.ProcessedFiles == e.TotalFiles))
        {
            _logger.LogDebug(
                "Processing progress: {ProcessedFiles}/{TotalFiles} ({Percentage:F1}%) - {Operation}",
                e.ProcessedFiles, e.TotalFiles, e.ProgressPercentage, e.CurrentOperation);
        }
    }

    /// <summary>
    /// Graceful shutdown handling.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing Coordinator Hosted Service stop requested");
        
        try
        {
            // Unsubscribe from events
            _coordinator.BatchProcessingStarted -= OnBatchProcessingStarted;
            _coordinator.BatchProcessingCompleted -= OnBatchProcessingCompleted;
            _coordinator.ProcessingProgress -= OnProcessingProgress;

            await base.StopAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Processing Coordinator Hosted Service");
        }
    }
}