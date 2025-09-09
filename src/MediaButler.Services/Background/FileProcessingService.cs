using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services.Background;

/// <summary>
/// Background service responsible for processing files from the queue.
/// Implements IHostedService for integration with the .NET hosting model,
/// following "Simple Made Easy" principles with clear separation of concerns.
/// </summary>
public class FileProcessingService : BackgroundService
{
    private readonly IFileProcessingQueue _queue;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<FileProcessingService> _logger;
    private readonly SemaphoreSlim _processingLimitSemaphore;
    
    // ARM32 optimization: limit concurrent processing
    private const int MaxConcurrentProcessing = 2;
    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan ErrorRetryDelay = TimeSpan.FromSeconds(5);

    public FileProcessingService(
        IFileProcessingQueue queue,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<FileProcessingService> logger)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // ARM32 resource management: limit concurrent file processing
        _processingLimitSemaphore = new SemaphoreSlim(MaxConcurrentProcessing, MaxConcurrentProcessing);
    }

    /// <summary>
    /// Main execution loop for the background service.
    /// Continuously processes files from the queue until cancellation is requested.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "File Processing Service started. Max concurrent processing: {MaxConcurrent}",
            MaxConcurrentProcessing);

        try
        {
            // Main processing loop
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for available processing slot (ARM32 resource management)
                    await _processingLimitSemaphore.WaitAsync(stoppingToken);

                    try
                    {
                        // Dequeue next file for processing
                        var file = await _queue.DequeueAsync(stoppingToken);
                        
                        if (file == null)
                        {
                            _logger.LogDebug("No file dequeued, continuing...");
                            continue;
                        }

                        // Process file in background task to maintain queue throughput
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await ProcessFileAsync(file, stoppingToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex,
                                    "Unhandled error processing file {FileHash} ({FileName})",
                                    file.Hash, file.FileName);
                            }
                            finally
                            {
                                _processingLimitSemaphore.Release();
                            }
                        }, stoppingToken);
                    }
                    catch
                    {
                        // Release semaphore if we didn't start processing
                        _processingLimitSemaphore.Release();
                        throw;
                    }

                    // Small delay to prevent CPU spinning
                    await Task.Delay(ProcessingDelay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("File processing service cancellation requested");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in file processing service main loop");
                    
                    // Brief delay before retrying to prevent error loops
                    await Task.Delay(ErrorRetryDelay, stoppingToken);
                }
            }
        }
        finally
        {
            _logger.LogInformation(
                "File Processing Service stopped. Queue size: {QueueSize}, High priority: {HighPrioritySize}",
                _queue.Count, _queue.HighPriorityCount);
        }
    }

    /// <summary>
    /// Processes a single file through the classification and organization pipeline.
    /// Uses scoped services to ensure proper resource cleanup and transaction boundaries.
    /// </summary>
    private async Task ProcessFileAsync(TrackedFile file, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Starting processing for file {FileHash} ({FileName})",
            file.Hash, file.FileName);

        try
        {
            // Create service scope for proper dependency injection lifecycle
            using var scope = _serviceScopeFactory.CreateScope();
            var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
            
            // TODO: Integrate ML classification service when available
            // For now, simulate classification with placeholder category
            var placeholderCategory = "UNCATEGORIZED";
            var placeholderConfidence = 0.5m; // Low confidence to trigger manual review
            
            // Update file with classification results
            var classificationResult = await fileService.UpdateClassificationAsync(
                file.Hash, placeholderCategory, placeholderConfidence, cancellationToken);
            
            if (classificationResult.IsSuccess)
            {
                _logger.LogInformation(
                    "Successfully processed file {FileHash} ({FileName}). Category: {Category}, Confidence: {Confidence}",
                    file.Hash, file.FileName, placeholderCategory, placeholderConfidence);
            }
            else
            {
                _logger.LogError(
                    "Failed to update file {FileHash} classification: {Error}",
                    file.Hash, classificationResult.Error);
                
                // Record processing error
                await fileService.RecordErrorAsync(
                    file.Hash, 
                    $"Classification failed: {classificationResult.Error}",
                    null,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(
                "Processing cancelled for file {FileHash} ({FileName})",
                file.Hash, file.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing file {FileHash} ({FileName})",
                file.Hash, file.FileName);

            try
            {
                // Record the error using the proper service method
                using var scope = _serviceScopeFactory.CreateScope();
                var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
                
                await fileService.RecordErrorAsync(
                    file.Hash, 
                    ex.Message,
                    ex.ToString(),
                    cancellationToken);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx,
                    "Failed to record error for file {FileHash} after processing failure",
                    file.Hash);
            }
        }
    }

    /// <summary>
    /// Graceful shutdown handling to complete processing of current files.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("File Processing Service stop requested");
        
        try
        {
            // Wait for current processing to complete with timeout
            var timeout = TimeSpan.FromSeconds(30);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            // Wait for all processing slots to be available (indicating completion)
            for (int i = 0; i < MaxConcurrentProcessing; i++)
            {
                try
                {
                    await _processingLimitSemaphore.WaitAsync(cts.Token);
                    _processingLimitSemaphore.Release();
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Timeout waiting for file processing to complete during shutdown");
                    break;
                }
            }
        }
        finally
        {
            await base.StopAsync(cancellationToken);
        }
    }

    public override void Dispose()
    {
        _processingLimitSemaphore?.Dispose();
        base.Dispose();
    }
}