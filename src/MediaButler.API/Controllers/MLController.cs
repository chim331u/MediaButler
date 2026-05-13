using Microsoft.AspNetCore.Mvc;
using MediaButler.ML.Interfaces;
using MediaButler.ML.Models;
using MediaButler.Services.ML;
using System.Diagnostics;

namespace MediaButler.API.Controllers;

/// <summary>
/// Controller for Machine Learning operations (Micro-Kernel Architecture).
/// Exposes endpoints for Training, Prediction, and Management.
/// </summary>
[ApiController]
[Route("api/ml")]
public class MLController : ControllerBase
{
    private readonly ILogger<MLController> _logger;
    private readonly IMLModelManager _modelManager;
    private readonly IDatabaseTrainingService _trainingService;

    public MLController(
        ILogger<MLController> logger,
        IMLModelManager modelManager,
        IDatabaseTrainingService trainingService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _modelManager = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
        _trainingService = trainingService ?? throw new ArgumentNullException(nameof(trainingService));
    }

    /// <summary>
    /// Triggers a new model training session using data from the database.
    /// </summary>
    [HttpPost("train")]
    [ProducesResponseType(typeof(TrainedModelInfo), StatusCodes.Status200OK)]
    public async Task<IActionResult> TrainModel()
    {
        var sessionId = Guid.NewGuid().ToString();
        _logger.LogInformation("Creating new training session: {SessionId}", sessionId);

        var result = await _trainingService.TrainModelFromDatabaseAsync(sessionId);

        if (result.IsFailure)
        {
            return StatusCode(500, new { Error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Predicts the category for a single filename.
    /// Uses the Hot-Reloadable Kernel (MLModelManager).
    /// </summary>
    [HttpPost("categorize")]
    [ProducesResponseType(typeof(FileCategoryPrediction), StatusCodes.Status200OK)]
    public IActionResult Categorize([FromBody] FileCategoryInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Filename))
        {
            return BadRequest("Filename is required.");
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var prediction = _modelManager.Predict(input);
            stopwatch.Stop();

            _logger.LogInformation("Prediction for '{Filename}' -> {Category} ({Confidence:P0}) in {Elapsed}ms", 
                input.Filename, prediction.Category, prediction.Score.Max(), stopwatch.ElapsedMilliseconds);

            return Ok(prediction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Prediction failed for {Filename}", input.Filename);
            return StatusCode(500, new { Error = "Prediction failed internally." });
        }
    }

    /// <summary>
    /// Bulk prediction for multiple filenames.
    /// </summary>
    [HttpPost("categorize/bulk")]
    [ProducesResponseType(typeof(List<FileCategoryPrediction>), StatusCodes.Status200OK)]
    public IActionResult CategorizeBulk([FromBody] List<FileCategoryInput> inputs)
    {
        if (inputs == null || !inputs.Any())
        {
            return BadRequest("Input list is empty.");
        }

        var results = new List<object>();

        foreach (var input in inputs)
        {
            try 
            {
                var prediction = _modelManager.Predict(input);
                results.Add(new 
                { 
                    Input = input,
                    Prediction = prediction
                });
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Failed to predict for {Filename}", input.Filename);
                results.Add(new 
                { 
                    Input = input, 
                    Error = "Failed" 
                });
            }
        }

        return Ok(results);
    }

    /// <summary>
    /// Manually triggers a model reload (Hot Swap).
    /// </summary>
    [HttpPost("reload")]
    public IActionResult ReloadModel()
    {
        _modelManager.ReloadModel();
        return Ok(new { Message = "Model reload triggered." });
    }
}
