using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using MediaButler.Core.Services;
using MediaButler.Services.Interfaces;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services.Background;

/// <summary>
/// Background service responsible for processing files through ML classification and intelligent organization.
/// Implements the complete pipeline: file dequeue → ML classification → automatic organization (high confidence) 
/// or staging for user confirmation (medium confidence). Implements IHostedService for integration with the 
/// .NET hosting model, following "Simple Made Easy" principles with clear separation of concerns.
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
    /// Processes a single file through the complete ML classification and file organization pipeline.
    /// Uses scoped services to ensure proper resource cleanup and transaction boundaries.
    /// Integrates ML classification results with intelligent file organization based on confidence levels.
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
            var predictionService = scope.ServiceProvider.GetRequiredService<IPredictionService>();
            var fileOrganizationService = scope.ServiceProvider.GetRequiredService<IFileOrganizationService>();
            
            // Perform ML classification using the filename
            _logger.LogDebug("Starting ML classification for file: {FileName}", file.FileName);
            var mlResult = await predictionService.PredictAsync(file.FileName, cancellationToken);
            
            if (!mlResult.IsSuccess)
            {
                _logger.LogWarning("ML classification failed for {FileName}: {Error}", 
                    file.FileName, mlResult.Error);
                
                // Fallback to manual categorization
                var fallbackResult = await fileService.UpdateClassificationAsync(
                    file.Hash, "UNCATEGORIZED", 0.3m, cancellationToken);
                
                if (!fallbackResult.IsSuccess)
                {
                    await fileService.RecordErrorAsync(file.Hash, 
                        $"Both ML classification and fallback failed: {mlResult.Error}", 
                        null, cancellationToken);
                }
                return;
            }
            
            var classification = mlResult.Value;
            var confidence = (decimal)classification.Confidence;
            
            // Update file with ML classification results
            var classificationResult = await fileService.UpdateClassificationAsync(
                file.Hash, classification.PredictedCategory, confidence, cancellationToken);
            
            if (classificationResult.IsSuccess)
            {
                _logger.LogInformation(
                    "Successfully classified file {FileHash} ({FileName}). Category: {Category}, Confidence: {Confidence:F2}, Decision: {Decision}",
                    file.Hash, file.FileName, classification.PredictedCategory, confidence, classification.Decision);

                // Step 2: File Organization - Automatically organize high-confidence classifications
                await ProcessFileOrganizationAsync(file, classification, fileOrganizationService, cancellationToken);
            }
            else
            {
                _logger.LogError(
                    "Failed to update file {FileHash} classification: {Error}",
                    file.Hash, classificationResult.Error);
                
                // Record processing error
                await fileService.RecordErrorAsync(
                    file.Hash, 
                    $"Classification update failed: {classificationResult.Error}",
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

            // Use error classification to determine recovery strategy
            await HandleProcessingErrorAsync(file, ex, cancellationToken);
        }
    }

    /// <summary>
    /// Handles processing errors using intelligent error classification and recovery strategies.
    /// Implements retry logic for transient errors and records user intervention requirements for others.
    /// </summary>
    private async Task HandleProcessingErrorAsync(TrackedFile file, Exception ex, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var fileService = scope.ServiceProvider.GetRequiredService<IFileService>();
            var errorClassificationService = scope.ServiceProvider.GetRequiredService<IErrorClassificationService>();

            // Create error context for classification
            var errorContext = new Core.Models.ErrorContext
            {
                Exception = ex,
                OperationType = "FileProcessing",
                SourcePath = file.OriginalPath,
                FileHash = file.Hash,
                FileSize = file.FileSize,
                RetryAttempts = file.RetryCount,
                AdditionalContext = new Dictionary<string, object>
                {
                    ["FileName"] = file.FileName,
                    ["Status"] = file.Status.ToString(),
                    ["ProcessingAttempt"] = DateTime.UtcNow
                }
            };

            // Classify the error to determine recovery strategy
            var classificationResult = await errorClassificationService.ClassifyErrorAsync(errorContext);
            
            if (classificationResult.IsSuccess)
            {
                var classification = classificationResult.Value;
                _logger.LogInformation(
                    "Error classified for file {FileHash}: Type={ErrorType}, CanRetry={CanRetry}, RequiresIntervention={RequiresIntervention}",
                    file.Hash, classification.ErrorType, classification.CanRetry, classification.RequiresUserIntervention);

                // Determine recovery action
                var recoveryResult = await errorClassificationService.DetermineRecoveryActionAsync(errorContext, classification);
                
                if (recoveryResult.IsSuccess)
                {
                    var recoveryAction = recoveryResult.Value;
                    
                    // Apply recovery strategy based on classification
                    await ApplyRecoveryStrategyAsync(file, classification, recoveryAction, fileService, cancellationToken);
                    
                    // Record error outcome for learning
                    await errorClassificationService.RecordErrorOutcomeAsync(
                        errorContext, classification, recoveryAction, wasSuccessful: true);
                }
                else
                {
                    _logger.LogWarning("Failed to determine recovery action for file {FileHash}: {Error}",
                        file.Hash, recoveryResult.Error);
                }
            }
            else
            {
                _logger.LogWarning("Failed to classify error for file {FileHash}: {Error}",
                    file.Hash, classificationResult.Error);
            }

            // Always record the error for audit trail
            await fileService.RecordErrorAsync(
                file.Hash,
                ex.Message,
                ex.ToString(),
                cancellationToken);
        }
        catch (Exception handlingEx)
        {
            _logger.LogError(handlingEx,
                "Failed to handle processing error for file {FileHash}",
                file.Hash);
        }
    }

    /// <summary>
    /// Applies the appropriate recovery strategy based on error classification.
    /// Uses existing services and focuses on recording error information for user action.
    /// </summary>
    private async Task ApplyRecoveryStrategyAsync(
        TrackedFile file, 
        Core.Models.ErrorClassificationResult classification,
        Core.Models.ErrorRecoveryAction recoveryAction,
        IFileService fileService,
        CancellationToken cancellationToken)
    {
        var errorDetails = $"Error Type: {classification.ErrorType}, " +
                          $"User Message: {classification.UserMessage}, " +
                          $"Can Retry: {classification.CanRetry}, " +
                          $"Requires Intervention: {classification.RequiresUserIntervention}";

        switch (classification.ErrorType)
        {
            case Core.Enums.FileOperationErrorType.TransientError:
                // Transient errors: log for retry by existing background service logic
                if (file.RetryCount < classification.MaxRetryAttempts)
                {
                    _logger.LogInformation(
                        "File {FileHash} eligible for retry (attempt {RetryCount}/{MaxRetries}). " +
                        "Background service will retry with delay: {DelayMs}ms",
                        file.Hash, file.RetryCount + 1, classification.MaxRetryAttempts, classification.RecommendedRetryDelayMs);
                    
                    // Record as retryable error - existing retry logic will handle the actual retry
                    await fileService.RecordErrorAsync(file.Hash, 
                        $"Transient error - retry recommended: {classification.UserMessage}", 
                        errorDetails, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("File {FileHash} exceeded max retry attempts", file.Hash);
                    await fileService.RecordErrorAsync(file.Hash, 
                        "Max retry attempts exceeded - manual intervention required", 
                        errorDetails, cancellationToken);
                }
                break;

            case Core.Enums.FileOperationErrorType.PermissionError:
            case Core.Enums.FileOperationErrorType.SpaceError:
            case Core.Enums.FileOperationErrorType.PathError:
                // These require user intervention - record detailed error information
                _logger.LogWarning(
                    "File {FileHash} requires user intervention: {ErrorType} - {Message}",
                    file.Hash, classification.ErrorType, classification.UserMessage);
                
                await fileService.RecordErrorAsync(file.Hash, 
                    $"User intervention required: {classification.UserMessage}", 
                    errorDetails, cancellationToken);
                break;

            case Core.Enums.FileOperationErrorType.UnknownError:
            default:
                // Unknown errors - record for manual investigation
                _logger.LogError(
                    "File {FileHash} encountered unknown error and requires manual investigation",
                    file.Hash);
                
                await fileService.RecordErrorAsync(file.Hash, 
                    $"Unknown error requires investigation: {classification.UserMessage}", 
                    errorDetails, cancellationToken);
                break;
        }
    }

    /// <summary>
    /// Processes file organization after successful ML classification.
    /// Implements intelligent organization logic based on confidence scores and classification decisions.
    /// </summary>
    private async Task ProcessFileOrganizationAsync(TrackedFile file, ClassificationResult classification, 
        IFileOrganizationService organizationService, CancellationToken cancellationToken)
    {
        try
        {
            // Parse classification decision and confidence for organization logic
            var confidence = (decimal)classification.Confidence;
            var category = classification.PredictedCategory;
            var decision = classification.Decision;

            _logger.LogDebug("Processing organization for file {FileHash} - Category: {Category}, Confidence: {Confidence}, Decision: {Decision}",
                file.Hash, category, confidence, decision);

            // Organization logic based on ML confidence and decision
            if (decision == ClassificationDecision.AutoClassify && confidence >= 0.85m)
            {
                // High confidence: Auto-organize immediately
                _logger.LogInformation("Auto-organizing high-confidence file {FileHash} to category {Category}", 
                    file.Hash, category);

                var organizationResult = await organizationService.OrganizeFileAsync(file.Hash, category);
                
                if (organizationResult.IsSuccess)
                {
                    _logger.LogInformation("Successfully auto-organized file {FileHash} ({FileName}) to {TargetPath}",
                        file.Hash, file.FileName, organizationResult.Value.TargetPath);
                }
                else
                {
                    _logger.LogWarning("Auto-organization failed for file {FileHash}: {Error}. File remains classified for manual review.",
                        file.Hash, organizationResult.Error);
                }
            }
            else if (decision == ClassificationDecision.SuggestWithAlternatives && confidence >= 0.50m)
            {
                // Medium confidence: Preview and stage for user confirmation
                _logger.LogInformation("Staging medium-confidence file {FileHash} for user confirmation", file.Hash);

                var previewResult = await organizationService.PreviewOrganizationAsync(file.Hash, category);
                
                if (previewResult.IsSuccess)
                {
                    _logger.LogDebug("Preview created for file {FileHash} - Target: {ProposedPath}, Safe: {IsSafe}",
                        file.Hash, previewResult.Value.ProposedPath, previewResult.Value.IsSafe);
                    
                    // File remains in "Classified" state for user to review and confirm
                    // The preview information can be accessed through the organization service when needed
                }
                else
                {
                    _logger.LogWarning("Failed to create organization preview for file {FileHash}: {Error}",
                        file.Hash, previewResult.Error);
                }
            }
            else
            {
                // Low confidence or other decisions: Leave for manual categorization
                var reasonMsg = decision switch
                {
                    ClassificationDecision.RequestManualCategorization => "likely new series",
                    ClassificationDecision.Unreliable => "unreliable classification",
                    ClassificationDecision.Failed => "classification failed",
                    _ => "requires manual review"
                };
                
                _logger.LogInformation("File {FileHash} requires manual categorization - Confidence: {Confidence}, Decision: {Decision}, Reason: {Reason}",
                    file.Hash, confidence, decision, reasonMsg);
                
                // File remains in "Classified" state with the ML suggestion for user to review
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during file organization for {FileHash}: {Error}", file.Hash, ex.Message);
            
            // Organization failure doesn't block the overall processing
            // File remains classified and available for manual organization
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