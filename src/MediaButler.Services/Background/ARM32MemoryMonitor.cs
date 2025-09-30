using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MediaButler.Services.Background;

/// <summary>
/// ARM32-specific memory monitoring service for QNAP TS-231P NAS with 1GB RAM.
/// Provides critical memory management to prevent system slowdowns and OOM conditions.
/// </summary>
/// <remarks>
/// This service implements aggressive memory monitoring tailored for ARM32 constraints:
/// - 200MB threshold for MediaButler application
/// - Proactive garbage collection
/// - Operation throttling when memory pressure detected
/// - Real-time memory usage tracking
/// </remarks>
public class ARM32MemoryMonitor
{
    private readonly ILogger _logger;
    private readonly long _memoryThresholdBytes;
    private int _operationCount;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes ARM32 memory monitor with 200MB threshold for QNAP TS-231P.
    /// </summary>
    /// <param name="logger">Logger for memory monitoring events.</param>
    /// <param name="memoryThresholdMB">Memory threshold in MB (default: 200MB for ARM32).</param>
    public ARM32MemoryMonitor(ILogger logger, int memoryThresholdMB = 200)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryThresholdBytes = memoryThresholdMB * 1024L * 1024L; // Convert to bytes

        _logger.LogInformation("ARM32 Memory Monitor initialized with {ThresholdMB}MB threshold for QNAP TS-231P", memoryThresholdMB);
    }

    /// <summary>
    /// Checks if processing should be throttled due to memory pressure.
    /// </summary>
    /// <returns>True if memory usage exceeds ARM32 safe threshold.</returns>
    public bool ShouldThrottleProcessing()
    {
        var currentMemoryUsage = GC.GetTotalMemory(false);
        var shouldThrottle = currentMemoryUsage > _memoryThresholdBytes;

        if (shouldThrottle)
        {
            var memoryUsageMB = currentMemoryUsage / (1024.0 * 1024.0);
            _logger.LogWarning("ARM32 memory pressure detected: {MemoryUsageMB:F1}MB > {ThresholdMB}MB - throttling operations",
                memoryUsageMB, _memoryThresholdBytes / (1024.0 * 1024.0));
        }

        return shouldThrottle;
    }

    /// <summary>
    /// Waits for memory to become available before proceeding with operations.
    /// Implements aggressive GC collection for ARM32 memory management.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the wait operation.</param>
    /// <returns>True if memory became available; false if cancelled.</returns>
    public async Task<bool> WaitForMemoryAvailableAsync(CancellationToken cancellationToken = default)
    {
        const int maxWaitIterations = 10;
        var waitIteration = 0;

        while (ShouldThrottleProcessing() && !cancellationToken.IsCancellationRequested && waitIteration < maxWaitIterations)
        {
            waitIteration++;
            var beforeMemory = GC.GetTotalMemory(false) / (1024.0 * 1024.0);

            _logger.LogInformation("ARM32 memory cleanup attempt {Iteration}/{MaxIterations} - before: {BeforeMemoryMB:F1}MB",
                waitIteration, maxWaitIterations, beforeMemory);

            // Aggressive garbage collection for ARM32
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true);

            var afterMemory = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
            var freedMemory = beforeMemory - afterMemory;

            _logger.LogInformation("ARM32 memory cleanup completed - after: {AfterMemoryMB:F1}MB, freed: {FreedMemoryMB:F1}MB",
                afterMemory, freedMemory);

            // Wait for system to stabilize
            await Task.Delay(1000, cancellationToken);
        }

        var finalCheck = !ShouldThrottleProcessing();
        if (!finalCheck && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("ARM32 memory pressure could not be resolved after {MaxIterations} attempts - system may be unstable", maxWaitIterations);
        }

        return finalCheck;
    }

    /// <summary>
    /// Tracks operation count and triggers periodic garbage collection every 100 operations.
    /// Critical for ARM32 memory management on QNAP TS-231P.
    /// </summary>
    public void TrackOperation()
    {
        lock (_lock)
        {
            _operationCount++;

            // Force GC every 100 operations for ARM32
            if (_operationCount % 100 == 0)
            {
                var beforeMemory = GC.GetTotalMemory(false) / (1024.0 * 1024.0);

                GC.Collect(1, GCCollectionMode.Optimized, false);

                var afterMemory = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
                var freedMemory = beforeMemory - afterMemory;

                _logger.LogDebug("ARM32 periodic GC after {OperationCount} operations - freed {FreedMemoryMB:F1}MB (before: {BeforeMemoryMB:F1}MB, after: {AfterMemoryMB:F1}MB)",
                    _operationCount, freedMemory, beforeMemory, afterMemory);
            }
        }
    }

    /// <summary>
    /// Gets current memory usage statistics for monitoring.
    /// </summary>
    /// <returns>Memory usage information for ARM32 monitoring.</returns>
    public ARM32MemoryStats GetMemoryStats()
    {
        var currentMemory = GC.GetTotalMemory(false);
        var isUnderPressure = currentMemory > _memoryThresholdBytes;

        return new ARM32MemoryStats
        {
            CurrentMemoryUsageMB = currentMemory / (1024.0 * 1024.0),
            ThresholdMB = _memoryThresholdBytes / (1024.0 * 1024.0),
            IsUnderMemoryPressure = isUnderPressure,
            OperationCount = _operationCount,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2)
        };
    }

    /// <summary>
    /// Forces immediate memory cleanup for critical ARM32 situations.
    /// Use sparingly as this is expensive on ARM32 processors.
    /// </summary>
    public void ForceMemoryCleanup()
    {
        var beforeMemory = GC.GetTotalMemory(false) / (1024.0 * 1024.0);

        _logger.LogWarning("ARM32 force memory cleanup initiated - before: {BeforeMemoryMB:F1}MB", beforeMemory);

        // Aggressive cleanup for ARM32
        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true);

        var afterMemory = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
        var freedMemory = beforeMemory - afterMemory;

        _logger.LogWarning("ARM32 force memory cleanup completed - after: {AfterMemoryMB:F1}MB, freed: {FreedMemoryMB:F1}MB",
            afterMemory, freedMemory);
    }
}

/// <summary>
/// Memory usage statistics for ARM32 monitoring and diagnostics.
/// </summary>
public class ARM32MemoryStats
{
    /// <summary>Current memory usage in megabytes.</summary>
    public double CurrentMemoryUsageMB { get; set; }

    /// <summary>Memory threshold in megabytes.</summary>
    public double ThresholdMB { get; set; }

    /// <summary>Whether system is under memory pressure.</summary>
    public bool IsUnderMemoryPressure { get; set; }

    /// <summary>Total operation count since initialization.</summary>
    public int OperationCount { get; set; }

    /// <summary>Generation 0 garbage collection count.</summary>
    public int Gen0Collections { get; set; }

    /// <summary>Generation 1 garbage collection count.</summary>
    public int Gen1Collections { get; set; }

    /// <summary>Generation 2 garbage collection count.</summary>
    public int Gen2Collections { get; set; }
}