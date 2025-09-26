using Microsoft.AspNetCore.Mvc;
using MediaButler.Core.Enums;
using MediaButler.Services.Interfaces;
using MediaButler.ML.Interfaces;

namespace MediaButler.API.Controllers;

/// <summary>
/// Controller for processing queue operations and status
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProcessingController : ControllerBase
{
    private readonly ILogger<ProcessingController> _logger;
    private readonly IFileService _fileService;
    private readonly IClassificationService _classificationService;

    public ProcessingController(ILogger<ProcessingController> logger, IFileService fileService, IClassificationService classificationService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _classificationService = classificationService ?? throw new ArgumentNullException(nameof(classificationService));
    }

    /// <summary>
    /// Gets the current processing queue status
    /// </summary>
    /// <returns>Processing queue status information</returns>
    [HttpGet("queue/status")]
    public async Task<ActionResult<ProcessingQueueStatus>> GetQueueStatus()
    {
        try
        {
            // For now, return mock data since we need to implement the actual processing service
            var status = new ProcessingQueueStatus
            {
                QueueSize = 5,
                ActiveJobs = 2,
                CompletedToday = 15,
                FailedToday = 1,
                AvgProcessingTimeMs = 2500,
                LastActivity = DateTime.UtcNow.AddMinutes(-5)
            };

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving processing queue status");
            return StatusCode(500, new { error = $"Failed to retrieve queue status: {ex.Message}" });
        }
    }

    /// <summary>
    /// Queues files for ML evaluation/re-evaluation based on specified statuses.
    /// Updates SuggestedCategory field for files in New, Classified, and ReadyToMove status.
    /// </summary>
    /// <param name="request">ML evaluation request parameters</param>
    /// <returns>Result of the ML evaluation operation</returns>
    /// <response code="200">ML evaluation queued successfully</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("ml-evaluation/queue")]
    [ProducesResponseType(typeof(MlEvaluationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MlEvaluationResponse>> QueueMlEvaluation([FromBody] MlEvaluationRequest request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new { error = "Request body is required" });
            }

            // Define the statuses that are eligible for ML evaluation
            var eligibleStatuses = new[] { FileStatus.New, FileStatus.Classified };

            _logger.LogInformation("Starting ML evaluation queue operation for statuses: {Statuses}",
                string.Join(", ", eligibleStatuses));

            // Get files with eligible statuses
            var result = await _fileService.GetFilesPagedByStatusesAsync(
                skip: 0,
                take: 1000, // Process up to 1000 files at once
                statuses: eligibleStatuses,
                category: request.FilterByCategory
            );

            if (!result.IsSuccess || result.Value == null)
            {
                _logger.LogWarning("Failed to retrieve files for ML evaluation: {Error}", result.Error);
                return StatusCode(500, new { error = $"Failed to retrieve files: {result.Error}" });
            }

            var filesToProcess = result.Value.ToList();
            var totalFiles = filesToProcess.Count;

            if (totalFiles == 0)
            {
                _logger.LogInformation("No files found for ML evaluation with specified criteria");
                return Ok(new MlEvaluationResponse
                {
                    Success = true,
                    TotalFilesQueued = 0,
                    Message = "No files found matching the criteria for ML evaluation",
                    QueuedAt = DateTime.UtcNow
                });
            }

            // Check if ML model is ready before processing
            if (!_classificationService.IsModelReady())
            {
                _logger.LogWarning("ML model is not ready for classification");
                return StatusCode(503, new { error = "ML classification service is not ready. Please try again later." });
            }

            _logger.LogInformation("Starting ML evaluation for {TotalFiles} files", totalFiles);

            var processedFiles = 0;
            var failedFiles = 0;
            var errors = new List<string>();

            // Process files in batches to avoid overwhelming the system
            const int batchSize = 10;
            for (int i = 0; i < filesToProcess.Count; i += batchSize)
            {
                var batch = filesToProcess.Skip(i).Take(batchSize).ToList();
                var filenames = batch.Select(f => f.FileName).ToList();

                try
                {
                    _logger.LogInformation("Processing batch {BatchStart}-{BatchEnd} of {TotalFiles}",
                        i + 1, Math.Min(i + batchSize, totalFiles), totalFiles);

                    // Classify the batch of filenames
                    var classificationResult = await _classificationService.ClassifyBatchAsync(filenames);

                    if (classificationResult.IsSuccess && classificationResult.Value != null)
                    {
                        var results = classificationResult.Value.ToList();

                        // Update each file with ML classification results
                        for (int j = 0; j < batch.Count && j < results.Count; j++)
                        {
                            var file = batch[j];
                            var mlResult = results[j];

                            try
                            {
                                // Update the file with ML classification results using the service
                                var updateResult = await _fileService.UpdateClassificationAsync(
                                    file.Hash,
                                    mlResult.PredictedCategory,
                                    (decimal)mlResult.Confidence
                                );

                                if (updateResult.IsSuccess)
                                {
                                    _logger.LogInformation("Updated file {FileName} with ML prediction: {Category} (confidence: {Confidence}%)",
                                        file.FileName, mlResult.PredictedCategory, mlResult.ConfidencePercentage);

                                    processedFiles++;
                                }
                                else
                                {
                                    var error = $"Failed to update file {file.FileName} classification: {updateResult.Error}";
                                    _logger.LogWarning(error);
                                    errors.Add(error);
                                    failedFiles++;
                                }
                            }
                            catch (Exception ex)
                            {
                                var error = $"Failed to update file {file.FileName}: {ex.Message}";
                                _logger.LogError(ex, "Error updating file with ML results");
                                errors.Add(error);
                                failedFiles++;
                            }
                        }
                    }
                    else
                    {
                        var error = $"ML classification failed for batch {i + 1}: {classificationResult.Error}";
                        _logger.LogError(error);
                        errors.Add(error);
                        failedFiles += batch.Count;
                    }
                }
                catch (Exception ex)
                {
                    var error = $"Exception processing batch {i + 1}: {ex.Message}";
                    _logger.LogError(ex, "Exception during ML batch processing");
                    errors.Add(error);
                    failedFiles += batch.Count;
                }
            }

            var response = new MlEvaluationResponse
            {
                Success = processedFiles > 0,
                TotalFilesQueued = processedFiles,
                Message = processedFiles > 0
                    ? $"Successfully processed {processedFiles} files with ML evaluation. {failedFiles} files failed."
                    : $"ML evaluation failed for all files. {string.Join("; ", errors.Take(3))}",
                QueuedAt = DateTime.UtcNow,
                EstimatedProcessingTimeMinutes = 0 // Processing is now complete
            };

            _logger.LogInformation("ML evaluation queue operation completed. Files queued: {TotalFiles}", totalFiles);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while queuing files for ML evaluation");
            return StatusCode(500, new { error = $"Failed to queue ML evaluation: {ex.Message}" });
        }
    }

    private static int CalculateEstimatedProcessingTime(int fileCount)
    {
        // Estimate 5 seconds per file for ML processing
        var totalSeconds = fileCount * 5;
        return (int)Math.Ceiling(totalSeconds / 60.0); // Convert to minutes
    }
}

/// <summary>
/// Processing queue status information
/// </summary>
public record ProcessingQueueStatus
{
    public int QueueSize { get; init; }
    public int ActiveJobs { get; init; }
    public int CompletedToday { get; init; }
    public int FailedToday { get; init; }
    public int AvgProcessingTimeMs { get; init; }
    public DateTime LastActivity { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Request for queuing ML evaluation
/// </summary>
public record MlEvaluationRequest
{
    /// <summary>
    /// Optional category filter. If provided, only files in this category will be processed.
    /// </summary>
    public string? FilterByCategory { get; init; }

    /// <summary>
    /// Force re-evaluation even if files already have a SuggestedCategory.
    /// </summary>
    public bool ForceReEvaluation { get; init; } = true;
}

/// <summary>
/// Response for ML evaluation queue operation
/// </summary>
public record MlEvaluationResponse
{
    /// <summary>
    /// Indicates if the operation was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Total number of files queued for ML evaluation
    /// </summary>
    public int TotalFilesQueued { get; init; }

    /// <summary>
    /// Descriptive message about the operation result
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp when the files were queued
    /// </summary>
    public DateTime QueuedAt { get; init; }

    /// <summary>
    /// Estimated processing time in minutes
    /// </summary>
    public int EstimatedProcessingTimeMinutes { get; init; }
}