using Microsoft.Extensions.Logging;

namespace MediaButler.API.Services;

/// <summary>
/// ARM32-optimized batch throttler to prevent resource exhaustion on low-spec NAS devices.
/// Adds configurable delays between batch operations to reduce memory pressure and CPU load.
/// Following "Simple Made Easy" - separates throttling concern from job execution.
/// </summary>
public class Arm32BatchThrottler : IBatchThrottler
{
    private readonly int _delayMs;
    private readonly ILogger<Arm32BatchThrottler> _logger;

    /// <summary>
    /// Creates a new ARM32 batch throttler with configurable delay.
    /// </summary>
    /// <param name="delayMs">Delay in milliseconds between operations (default: 50ms for ARM32)</param>
    /// <param name="logger">Logger instance</param>
    public Arm32BatchThrottler(ILogger<Arm32BatchThrottler> logger, int delayMs = 50)
    {
        _delayMs = delayMs;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("ARM32 batch throttler initialized with {DelayMs}ms delay", _delayMs);
    }

    public async Task ThrottleAsync(CancellationToken cancellationToken = default)
    {
        if (_delayMs > 0)
        {
            await Task.Delay(_delayMs, cancellationToken);
        }
    }

    public bool ShouldThrottle(int current, int total)
    {
        // Throttle between all operations except after the last one
        return current < total;
    }
}
