using System.Diagnostics;

namespace MediaButler.API.Middleware;

/// <summary>
/// Performance monitoring middleware with ARM32 optimization and automatic GC triggering.
/// Implements Sprint 1.4.3 requirements following "Simple Made Easy" principles.
/// </summary>
public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMonitoringMiddleware> _logger;
    private readonly IConfiguration _configuration;

    // ARM32 optimization settings from configuration
    private readonly int _memoryThresholdMB;
    private readonly int _autoGCTriggerMB;
    private readonly int _performanceThresholdMs;

    public PerformanceMonitoringMiddleware(
        RequestDelegate next, 
        ILogger<PerformanceMonitoringMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;

        // Load ARM32 optimization settings
        _memoryThresholdMB = configuration.GetValue<int>("Serilog:ARM32Optimization:MemoryThresholdMB", 300);
        _autoGCTriggerMB = configuration.GetValue<int>("Serilog:ARM32Optimization:AutoGCTriggerMB", 250);
        _performanceThresholdMs = configuration.GetValue<int>("Serilog:ARM32Optimization:PerformanceThresholdMs", 1000);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var initialMemory = GC.GetTotalMemory(false);
        var initialWorkingSet = Environment.WorkingSet;

        // Get correlation ID from context
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? "unknown";

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            await LogPerformanceMetricsAsync(context, correlationId, stopwatch, initialMemory, initialWorkingSet);
        }
    }

    private async Task LogPerformanceMetricsAsync(
        HttpContext context,
        string correlationId,
        Stopwatch stopwatch,
        long initialMemory,
        long initialWorkingSet)
    {
        var finalMemory = GC.GetTotalMemory(false);
        var finalWorkingSet = Environment.WorkingSet;
        var memoryDelta = finalMemory - initialMemory;
        var workingSetDelta = finalWorkingSet - initialWorkingSet;
        
        var finalMemoryMB = finalMemory / 1024.0 / 1024.0;
        var workingSetMB = finalWorkingSet / 1024.0 / 1024.0;
        var memoryDeltaKB = memoryDelta / 1024.0;

        // Add performance headers if response hasn't started
        if (!context.Response.HasStarted)
        {
            context.Response.Headers["X-Response-Time"] = $"{stopwatch.ElapsedMilliseconds}ms";
            context.Response.Headers["X-Memory-Delta"] = $"{memoryDeltaKB:F2}KB";
            context.Response.Headers["X-Memory-Total"] = $"{finalMemoryMB:F2}MB";
            context.Response.Headers["X-Working-Set"] = $"{workingSetMB:F2}MB";
            context.Response.Headers["X-Correlation-ID"] = correlationId;
        }

        // Structured performance logging
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["DurationMs"] = stopwatch.ElapsedMilliseconds,
            ["MemoryUsageMB"] = finalMemoryMB,
            ["WorkingSetMB"] = workingSetMB,
            ["MemoryDeltaKB"] = memoryDeltaKB,
            ["StatusCode"] = context.Response.StatusCode,
            ["RequestMethod"] = context.Request.Method,
            ["RequestPath"] = context.Request.Path.Value ?? ""
        });

        _logger.LogInformation(
            "Request {Method} {Path} completed in {Duration}ms with status {StatusCode}, Memory: {MemoryMB:F2}MB, Delta: {MemoryDelta:F2}KB",
            context.Request.Method,
            context.Request.Path.Value,
            stopwatch.ElapsedMilliseconds,
            context.Response.StatusCode,
            finalMemoryMB,
            memoryDeltaKB);

        // ARM32-specific monitoring and warnings
        await MonitorARM32ConstraintsAsync(context, stopwatch.ElapsedMilliseconds, finalMemoryMB, workingSetMB, memoryDelta);

        // Automatic GC triggering for high allocation requests
        if (memoryDelta > (_autoGCTriggerMB * 1024 * 1024))
        {
            _logger.LogWarning(
                "High memory allocation detected ({MemoryDelta:F2}MB), triggering garbage collection",
                memoryDelta / 1024.0 / 1024.0);
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var afterGCMemory = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
            _logger.LogInformation(
                "Garbage collection completed, memory reduced to {MemoryMB:F2}MB",
                afterGCMemory);
        }
    }

    private async Task MonitorARM32ConstraintsAsync(
        HttpContext context,
        long durationMs,
        double memoryMB,
        double workingSetMB,
        long memoryDelta)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? "unknown";

        // Memory threshold monitoring (ARM32 optimized)
        if (memoryMB > _memoryThresholdMB)
        {
            _logger.LogWarning(
                "ARM32 memory threshold exceeded: {MemoryMB:F2}MB (threshold: {ThresholdMB}MB) - Correlation: {CorrelationId}",
                memoryMB,
                _memoryThresholdMB,
                correlationId);
        }

        // Working set memory warnings (ARM32 constraint monitoring)
        if (workingSetMB > (_memoryThresholdMB * 0.8)) // 80% of threshold
        {
            _logger.LogWarning(
                "High working set memory usage: {WorkingSetMB:F2}MB (80% of {ThresholdMB}MB threshold) - Correlation: {CorrelationId}",
                workingSetMB,
                _memoryThresholdMB,
                correlationId);
        }

        // Performance threshold monitoring
        if (durationMs > _performanceThresholdMs)
        {
            _logger.LogWarning(
                "Slow request detected: {Method} {Path} took {Duration}ms (threshold: {ThresholdMs}ms) - Correlation: {CorrelationId}",
                context.Request.Method,
                context.Request.Path.Value,
                durationMs,
                _performanceThresholdMs,
                correlationId);
        }

        // Large memory allocation warnings
        var memoryDeltaMB = memoryDelta / 1024.0 / 1024.0;
        if (memoryDeltaMB > 10) // 10MB allocation threshold
        {
            _logger.LogWarning(
                "Large memory allocation: {Method} {Path} allocated {MemoryDelta:F2}MB - Correlation: {CorrelationId}",
                context.Request.Method,
                context.Request.Path.Value,
                memoryDeltaMB,
                correlationId);
        }

        // Generate memory pressure report for ARM32 monitoring
        if (memoryMB > (_memoryThresholdMB * 0.9)) // 90% of threshold
        {
            await LogMemoryPressureReportAsync(correlationId, memoryMB, workingSetMB);
        }
    }

    private async Task LogMemoryPressureReportAsync(string correlationId, double memoryMB, double workingSetMB)
    {
        var gcInfo = new
        {
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            TotalMemoryMB = memoryMB,
            WorkingSetMB = workingSetMB
        };

        _logger.LogWarning(
            "ARM32 Memory Pressure Report - Correlation: {CorrelationId}, {@GCInfo}",
            correlationId,
            gcInfo);

        // Proactive garbage collection when approaching limits
        if (memoryMB > (_memoryThresholdMB * 0.95))
        {
            _logger.LogError(
                "Critical memory usage detected: {MemoryMB:F2}MB (95% of {ThresholdMB}MB) - Forcing GC - Correlation: {CorrelationId}",
                memoryMB,
                _memoryThresholdMB,
                correlationId);

            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
        }

        await Task.CompletedTask;
    }
}

/// <summary>
/// Extension methods for registering the performance monitoring middleware.
/// </summary>
public static class PerformanceMonitoringMiddlewareExtensions
{
    public static IApplicationBuilder UsePerformanceMonitoring(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<PerformanceMonitoringMiddleware>();
    }
}