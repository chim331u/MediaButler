using Hangfire;
using MediaButler.Core.Models;
using MediaButler.Core.Services;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services.Background;

/// <summary>
/// Proxy class that can be referenced by API project to enqueue batch file processing jobs.
/// This avoids circular dependency by being in Services project.
/// The actual work is delegated to IBatchFileProcessor which is implemented in Batch project.
/// </summary>
[Queue("default")]
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
public class BatchFileProcessingJobProxy
{
    private readonly IBatchFileProcessor _processor;
    private readonly ILogger<BatchFileProcessingJobProxy> _logger;

    public BatchFileProcessingJobProxy(
        IBatchFileProcessor processor,
        ILogger<BatchFileProcessingJobProxy> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    /// <summary>
    /// Processes a batch of file organization operations.
    /// This method is called by Hangfire and delegates to the actual processor.
    /// </summary>
    public async Task ProcessBatchAsync(
        List<FileOrganizeOperation> operations,
        string batchName,
        string jobId,
        bool continueOnError,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "BatchFileProcessingJobProxy delegating to IBatchFileProcessor for job {JobId}",
            jobId);

        await _processor.ProcessBatchAsync(
            operations,
            batchName,
            jobId,
            continueOnError,
            cancellationToken);
    }
}
