using System.Collections.Concurrent;
using System.Diagnostics;
using MediaButler.Core.Common;
using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Core.Services;
using MediaButler.ML.Interfaces;
using MediaButler.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services.Background;

/// <summary>
/// Coordinates file processing operations with batch processing, prioritization, and resource management.
/// Orchestrates the ML classification pipeline following "Simple Made Easy" principles.
/// </summary>
public class ProcessingCoordinator : IProcessingCoordinator, IDisposable
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IMetricsCollectionService? _metricsService;
    private readonly ILogger<ProcessingCoordinator> _logger;
    
    // ARM32 optimized configuration
    private const int DefaultBatchSize = 10; // Small batch size for ARM32
    private const int MaxConcurrentBatches = 2; // Limit concurrent processing
    private const int ThrottlingThresholdMB = 250; // Memory threshold for throttling
    private const double BackpressureThreshold = 0.8; // 80% memory usage triggers backpressure
    
    private readonly SemaphoreSlim _processingLock;
    private readonly Timer _metricsTimer;
    private readonly ConcurrentDictionary<string, ProcessingMetric> _processingMetrics;
    
    private volatile bool _isRunning;
    private volatile bool _isThrottling;
    private volatile bool _disposed;
    
    // Statistics tracking
    private long _totalBatchesProcessed;
    private long _totalFilesProcessed;
    private double _totalProcessingTimeMs;
    private long _totalSuccessfulClassifications;
    private long _totalFailedClassifications;

    public ProcessingCoordinator(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ProcessingCoordinator> logger,
        IMetricsCollectionService? metricsService = null)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metricsService = metricsService;

        _processingLock = new SemaphoreSlim(MaxConcurrentBatches, MaxConcurrentBatches);
        _processingMetrics = new ConcurrentDictionary<string, ProcessingMetric>();
        
        // Setup metrics collection timer
        _metricsTimer = new Timer(CollectMetrics, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        
        _logger.LogInformation("Processing Coordinator initialized with batch size: {BatchSize}, max concurrent batches: {MaxBatches}",
            DefaultBatchSize, MaxConcurrentBatches);
    }

    /// <inheritdoc />
    public bool IsRunning => _isRunning;

    /// <inheritdoc />
    public event EventHandler<BatchProcessingStartedEventArgs>? BatchProcessingStarted;
    
    /// <inheritdoc />
    public event EventHandler<BatchProcessingCompletedEventArgs>? BatchProcessingCompleted;
    
    /// <inheritdoc />
    public event EventHandler<ProcessingProgressEventArgs>? ProcessingProgress;

    /// <inheritdoc />
    public Task<Result> StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return Task.FromResult(Result.Failure("Coordinator has been disposed"));

        if (_isRunning)
            return Task.FromResult(Result.Failure("Coordinator is already running"));

        _isRunning = true;
        _logger.LogInformation("Processing Coordinator started");
        
        return Task.FromResult(Result.Success());
    }

    /// <inheritdoc />
    public async Task<Result> StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
            return Result.Success();

        _logger.LogInformation("Stopping Processing Coordinator");
        
        try
        {
            // Wait for current processing to complete
            var timeout = TimeSpan.FromSeconds(30);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            // Wait for all processing slots to be available
            for (int i = 0; i < MaxConcurrentBatches; i++)
            {
                try
                {
                    await _processingLock.WaitAsync(cts.Token);
                    _processingLock.Release();
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Timeout waiting for processing to complete during shutdown");
                    break;
                }
            }

            _isRunning = false;
            _logger.LogInformation("Processing Coordinator stopped successfully");
            
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Processing Coordinator");
            return Result.Failure($"Error stopping coordinator: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<BatchProcessingResult>> ProcessBatchAsync(
        IEnumerable<TrackedFile> files, 
        CancellationToken cancellationToken = default)
    {
        return await ProcessBatchInternalAsync(files, isHighPriority: false, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<BatchProcessingResult>> ProcessHighPriorityBatchAsync(
        IEnumerable<TrackedFile> files, 
        CancellationToken cancellationToken = default)
    {
        return await ProcessBatchInternalAsync(files, isHighPriority: true, cancellationToken);
    }

    /// <summary>
    /// Internal batch processing implementation with priority support.
    /// </summary>
    private async Task<Result<BatchProcessingResult>> ProcessBatchInternalAsync(
        IEnumerable<TrackedFile> files,
        bool isHighPriority,
        CancellationToken cancellationToken)
    {
        if (_disposed || !_isRunning)
            return Result<BatchProcessingResult>.Failure("Coordinator is not running");

        var fileList = files.ToList();
        if (!fileList.Any())
            return Result<BatchProcessingResult>.Success(new BatchProcessingResult { TotalFiles = 0 });

        // Check for throttling conditions
        if (_isThrottling && !isHighPriority)
        {
            _logger.LogWarning("Processing throttled due to resource constraints. Batch size: {BatchSize}", fileList.Count);
            return Result<BatchProcessingResult>.Failure("Processing throttled due to resource constraints");
        }

        try
        {
            await _processingLock.WaitAsync(cancellationToken);
            try
            {
                return await ExecuteBatchProcessingAsync(fileList, isHighPriority, cancellationToken);
            }
            finally
            {
                _processingLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Batch processing cancelled");
            return Result<BatchProcessingResult>.Failure("Processing was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch processing");
            return Result<BatchProcessingResult>.Failure($"Batch processing error: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes the actual batch processing with progress tracking and error handling.
    /// </summary>
    private async Task<Result<BatchProcessingResult>> ExecuteBatchProcessingAsync(
        List<TrackedFile> files,
        bool isHighPriority,
        CancellationToken cancellationToken)
    {
        var batchId = Guid.NewGuid().ToString("N")[..8];
        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogInformation("Starting batch processing {BatchId}: {FileCount} files, Priority: {Priority}",
            batchId, files.Count, isHighPriority ? "HIGH" : "NORMAL");

        // Record batch processing start metrics
        if (_metricsService != null)
        {
            foreach (var file in files)
            {
                await _metricsService.RecordProcessingEventAsync(
                    ProcessingEventType.ClassificationStarted, 
                    file.Hash);
            }
        }

        // Fire batch started event
        BatchProcessingStarted?.Invoke(this, new BatchProcessingStartedEventArgs
        {
            BatchSize = files.Count,
            IsHighPriority = isHighPriority
        });

        var result = new BatchProcessingResult
        {
            TotalFiles = files.Count
        };
        
        var errors = new List<string>();
        var processedCount = 0;
        var successCount = 0;
        var confidenceSum = 0.0;

        try
        {
            // Process files in optimal batch sizes for ARM32
            var batches = files.Chunk(DefaultBatchSize);
            
            foreach (var batch in batches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // Update progress
                ProcessingProgress?.Invoke(this, new ProcessingProgressEventArgs
                {
                    TotalFiles = files.Count,
                    ProcessedFiles = processedCount,
                    CurrentBatchSize = batch.Length,
                    CurrentOperation = $"Processing batch {batchId}"
                });

                // Process batch with ML classification
                var batchResult = await ProcessFileBatch(batch, cancellationToken);
                
                processedCount += batch.Length;
                successCount += batchResult.SuccessCount;
                confidenceSum += batchResult.ConfidenceSum;
                errors.AddRange(batchResult.Errors);

                // Small delay between batches for ARM32 resource management
                if (processedCount < files.Count)
                {
                    await Task.Delay(50, cancellationToken);
                }
            }

            stopwatch.Stop();

            // Update result statistics
            result = new BatchProcessingResult
            {
                TotalFiles = result.TotalFiles,
                ProcessedFiles = processedCount,
                SuccessfulClassifications = successCount,
                FailedClassifications = processedCount - successCount,
                ProcessingDuration = stopwatch.Elapsed,
                AverageConfidence = successCount > 0 ? confidenceSum / successCount : 0.0,
                Errors = errors.AsReadOnly()
            };

            // Update global statistics
            Interlocked.Increment(ref _totalBatchesProcessed);
            Interlocked.Add(ref _totalFilesProcessed, processedCount);
            Interlocked.Add(ref _totalSuccessfulClassifications, successCount);
            Interlocked.Add(ref _totalFailedClassifications, processedCount - successCount);
            
            var processingTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            var currentAvg = _totalProcessingTimeMs;
            var newAvg = (currentAvg * (_totalBatchesProcessed - 1) + processingTimeMs) / _totalBatchesProcessed;
            Interlocked.Exchange(ref _totalProcessingTimeMs, newAvg);

            _logger.LogInformation(
                "Completed batch processing {BatchId}: {Processed}/{Total} files, Success rate: {SuccessRate:F1}%, Duration: {Duration}ms",
                batchId, successCount, files.Count, 
                files.Count > 0 ? (double)successCount / files.Count * 100 : 0,
                stopwatch.ElapsedMilliseconds);

            // Fire batch completed event
            BatchProcessingCompleted?.Invoke(this, new BatchProcessingCompletedEventArgs
            {
                Result = result,
                IsHighPriority = isHighPriority
            });

            return Result<BatchProcessingResult>.Success(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Batch processing {BatchId} cancelled", batchId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch processing {BatchId}", batchId);
            
            result = new BatchProcessingResult
            {
                TotalFiles = result.TotalFiles,
                ProcessedFiles = processedCount,
                SuccessfulClassifications = successCount,
                FailedClassifications = processedCount - successCount,
                ProcessingDuration = stopwatch.Elapsed,
                AverageConfidence = successCount > 0 ? confidenceSum / successCount : 0.0,
                Errors = errors.Concat(new[] { ex.Message }).ToList().AsReadOnly()
            };

            return Result<BatchProcessingResult>.Success(result);
        }
    }

    /// <summary>
    /// Processes a batch of files through the ML classification pipeline.
    /// </summary>
    private async Task<FileBatchResult> ProcessFileBatch(
        TrackedFile[] batch,
        CancellationToken cancellationToken)
    {
        var successCount = 0;
        var confidenceSum = 0.0;
        var errors = new List<string>();

        // Create scope for scoped service operations
        using var scope = _serviceScopeFactory.CreateScope();
        var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
        var predictionService = scope.ServiceProvider.GetRequiredService<IPredictionService>();

        // Extract filenames for batch ML prediction
        var filenames = batch.Select(f => f.FileName).ToList();
        
        try
        {
            // Use batch prediction for efficiency
            var batchStopwatch = Stopwatch.StartNew();
            var predictionResult = await predictionService.PredictBatchAsync(filenames, cancellationToken);
            batchStopwatch.Stop();

            // Record batch processing performance
            if (_metricsService != null)
            {
                await _metricsService.RecordPerformanceDataAsync(
                    OperationType.BatchProcessing,
                    batchStopwatch.Elapsed);
            }
            
            if (predictionResult.IsSuccess)
            {
                var predictions = predictionResult.Value.Results;
                
                // Update files with classification results
                for (int i = 0; i < batch.Length && i < predictions.Count; i++)
                {
                    var file = batch[i];
                    var prediction = predictions[i];
                    
                    try
                    {
                        var confidence = (decimal)prediction.Confidence;
                        var updateResult = await fileService.UpdateClassificationAsync(
                            file.Hash, prediction.PredictedCategory, confidence, cancellationToken);
                        
                        if (updateResult.IsSuccess)
                        {
                            successCount++;
                            confidenceSum += prediction.Confidence;
                            
                            _logger.LogDebug(
                                "Updated file {Hash} with category: {Category}, confidence: {Confidence:F2}",
                                file.Hash, prediction.PredictedCategory, confidence);
                        }
                        else
                        {
                            errors.Add($"Failed to update {file.FileName}: {updateResult.Error}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Error updating {file.FileName}: {ex.Message}");
                        _logger.LogWarning(ex, "Error updating file {Hash} classification", file.Hash);
                    }
                }
            }
            else
            {
                // Fallback to individual processing
                _logger.LogWarning("Batch prediction failed, falling back to individual processing: {Error}",
                    predictionResult.Error);
                
                foreach (var file in batch)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    try
                    {
                        var singleStopwatch = Stopwatch.StartNew();
                        var singleResult = await predictionService.PredictAsync(file.FileName, cancellationToken);
                        singleStopwatch.Stop();

                        // Record individual classification performance
                        if (_metricsService != null)
                        {
                            await _metricsService.RecordPerformanceDataAsync(
                                OperationType.MLClassification,
                                singleStopwatch.Elapsed);
                        }
                        
                        if (singleResult.IsSuccess)
                        {
                            var confidence = (decimal)singleResult.Value.Confidence;
                            var updateResult = await fileService.UpdateClassificationAsync(
                                file.Hash, singleResult.Value.PredictedCategory, confidence, cancellationToken);
                            
                            if (updateResult.IsSuccess)
                            {
                                successCount++;
                                confidenceSum += singleResult.Value.Confidence;
                            }
                            else
                            {
                                errors.Add($"Failed to update {file.FileName}: {updateResult.Error}");
                            }
                        }
                        else
                        {
                            errors.Add($"Failed to classify {file.FileName}: {singleResult.Error}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Error processing {file.FileName}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Batch processing error: {ex.Message}");
            _logger.LogError(ex, "Error in file batch processing");
        }

        return new FileBatchResult
        {
            SuccessCount = successCount,
            ConfidenceSum = confidenceSum,
            Errors = errors
        };
    }

    /// <inheritdoc />
    public async Task<Result<ProcessingCoordinatorStats>> GetStatisticsAsync()
    {
        if (_disposed)
            return Result<ProcessingCoordinatorStats>.Failure("Coordinator has been disposed");

        try
        {
            var stats = new ProcessingCoordinatorStats
            {
                TotalBatchesProcessed = (int)Interlocked.Read(ref _totalBatchesProcessed),
                TotalFilesProcessed = (int)Interlocked.Read(ref _totalFilesProcessed),
                AverageProcessingTimeMs = _totalProcessingTimeMs,
                SuccessRate = CalculateSuccessRate(),
                IsThrottling = _isThrottling,
                MemoryUsageMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0),
                LastProcessingTime = GetLastProcessingTime(),
                CurrentQueueSize = 0, // Would need queue integration
                CurrentHighPriorityQueueSize = 0 // Would need queue integration
            };

            return Result<ProcessingCoordinatorStats>.Success(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting coordinator statistics");
            return Result<ProcessingCoordinatorStats>.Failure($"Error getting statistics: {ex.Message}");
        }
    }

    /// <summary>
    /// Collects metrics and manages throttling based on system resources.
    /// </summary>
    private void CollectMetrics(object? state)
    {
        if (_disposed || !_isRunning)
            return;

        try
        {
            var memoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            
            // Throttling logic based on memory usage
            var shouldThrottle = memoryMB > ThrottlingThresholdMB;
            if (shouldThrottle != _isThrottling)
            {
                _isThrottling = shouldThrottle;
                _logger.LogInformation(
                    "Processing throttling {Status}. Memory usage: {MemoryMB:F1}MB",
                    _isThrottling ? "ENABLED" : "DISABLED", memoryMB);
            }

            // Force GC if memory usage is high
            if (memoryMB > ThrottlingThresholdMB * BackpressureThreshold)
            {
                GC.Collect(1, GCCollectionMode.Optimized);
                _logger.LogDebug("Triggered GC due to high memory usage: {MemoryMB:F1}MB", memoryMB);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting metrics");
        }
    }

    private double CalculateSuccessRate()
    {
        var total = Interlocked.Read(ref _totalFilesProcessed);
        var success = Interlocked.Read(ref _totalSuccessfulClassifications);
        return total > 0 ? (double)success / total * 100.0 : 0.0;
    }

    private DateTime GetLastProcessingTime()
    {
        // This would need proper tracking - for now return a reasonable default
        return _totalBatchesProcessed > 0 ? DateTime.UtcNow.AddMinutes(-5) : DateTime.MinValue;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _ = StopAsync();

        _metricsTimer?.Dispose();
        _processingLock?.Dispose();

        _logger.LogInformation("Processing Coordinator disposed");
    }

    /// <summary>
    /// Internal result structure for file batch processing.
    /// </summary>
    private record FileBatchResult
    {
        public int SuccessCount { get; init; }
        public double ConfidenceSum { get; init; }
        public List<string> Errors { get; init; } = new();
    }

    /// <summary>
    /// Internal processing metric for tracking operations.
    /// </summary>
    private record ProcessingMetric
    {
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public TimeSpan Duration { get; init; }
        public int FileCount { get; init; }
        public bool Success { get; init; }
    }
}