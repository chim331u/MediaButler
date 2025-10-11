namespace MediaButler.API.Services;

/// <summary>
/// Interface for throttling batch operations to prevent resource exhaustion.
/// Separates throttling concern from job execution logic following "Simple Made Easy" principles.
/// Critical for ARM32 deployments with limited resources (1GB RAM).
/// </summary>
public interface IBatchThrottler
{
    /// <summary>
    /// Applies throttling delay between batch operations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for graceful cancellation</param>
    Task ThrottleAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if throttling should be applied based on current progress.
    /// </summary>
    /// <param name="current">Current item number being processed</param>
    /// <param name="total">Total number of items</param>
    /// <returns>True if throttling should be applied, false otherwise</returns>
    bool ShouldThrottle(int current, int total);
}
