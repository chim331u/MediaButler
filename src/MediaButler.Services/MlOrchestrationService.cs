using MediaButler.Core.Enums;
using MediaButler.Core.Models.Requests;
using MediaButler.Core.Models.Responses;
using MediaButler.ML.Interfaces;
using MediaButler.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace MediaButler.Services;

/// <summary>
/// Implementation of the ML Orchestration Service.
/// Follows "Simple Made Easy" by separating orchestration logic from API controllers.
/// </summary>
public class MlOrchestrationService : IMlOrchestrationService
{
    private readonly ILogger<MlOrchestrationService> _logger;
    private readonly IFileService _fileService;
    private readonly IClassificationService _classificationService;

    public MlOrchestrationService(
        ILogger<MlOrchestrationService> logger,
        IFileService fileService,
        IClassificationService classificationService)
    {
        _logger = logger;
        _fileService = fileService;
        _classificationService = classificationService;
    }

    public async Task<MlEvaluationResponse> ProcessMlEvaluationBatchAsync(MlEvaluationRequest request)
    {
        // Define the statuses that are eligible for ML evaluation
        var eligibleStatuses = new[] { FileStatus.New, FileStatus.Classified };

        _logger.LogInformation("Starting ML evaluation orchestration for statuses: {Statuses}",
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
            throw new InvalidOperationException($"Failed to retrieve files: {result.Error}");
        }

        var filesToProcess = result.Value.Items.ToList();
        var totalFiles = result.Value.Total;

        if (totalFiles == 0)
        {
            return new MlEvaluationResponse
            {
                Success = true,
                TotalFilesQueued = 0,
                Message = "No files found matching the criteria for ML evaluation",
                QueuedAt = DateTime.UtcNow
            };
        }

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
                            var updateResult = await _fileService.UpdateClassificationAsync(
                                file.Hash,
                                mlResult.PredictedCategory,
                                (decimal)mlResult.Confidence
                            );

                            if (updateResult.IsSuccess)
                            {
                                processedFiles++;
                            }
                            else
                            {
                                errors.Add($"Failed to update {file.FileName}: {updateResult.Error}");
                                failedFiles++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error updating file {Hash}", file.Hash);
                            errors.Add($"Error updating {file.FileName}: {ex.Message}");
                            failedFiles++;
                        }
                    }
                }
                else
                {
                    errors.Add($"ML batch classification failed: {classificationResult.Error}");
                    failedFiles += batch.Count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during ML batch processing");
                errors.Add($"Batch error: {ex.Message}");
                failedFiles += batch.Count;
            }
        }

        return new MlEvaluationResponse
        {
            Success = processedFiles > 0,
            TotalFilesQueued = processedFiles,
            Message = processedFiles > 0
                ? $"Successfully processed {processedFiles} files. {failedFiles} failed."
                : $"Evaluation failed: {string.Join("; ", errors.Take(3))}",
            QueuedAt = DateTime.UtcNow
        };
    }

    public async Task<ClassificationResponse> ClassifyFilenameAsync(string filename)
    {
        _logger.LogInformation("Orchestrating classification for: {Filename}", filename);

        var classificationResult = await _classificationService.ClassifyFilenameAsync(filename);

        if (!classificationResult.IsSuccess || classificationResult.Value == null)
        {
            throw new InvalidOperationException($"Classification failed: {classificationResult.Error}");
        }

        var result = classificationResult.Value;

        // Build predictions list
        var predictions = new List<CategoryPredictionDto>
        {
            new CategoryPredictionDto
            {
                Category = result.PredictedCategory,
                Confidence = result.Confidence,
                ConfidencePercentage = Math.Round(result.Confidence * 100, 2),
                Rank = 1
            }
        };

        if (result.AlternativePredictions != null)
        {
            predictions.AddRange(result.AlternativePredictions.Select((alt, index) => new CategoryPredictionDto
            {
                Category = alt.Category,
                Confidence = alt.Confidence,
                ConfidencePercentage = Math.Round(alt.Confidence * 100, 2),
                Rank = index + 2
            }));
        }

        return new ClassificationResponse
        {
            Filename = filename,
            PredictedCategory = result.PredictedCategory,
            Confidence = result.Confidence,
            ConfidencePercentage = Math.Round(result.Confidence * 100, 2),
            Top5Predictions = predictions.Take(5).ToList(),
            ClassifiedAt = result.ClassifiedAt,
            ModelVersion = result.ModelVersion
        };
    }
}
