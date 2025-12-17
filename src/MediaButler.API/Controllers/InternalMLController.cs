using MediaButler.ML.Interfaces;
using MediaButler.ML.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MediaButler.API.Controllers;

[ApiController]
[Route("internal/ml")]
// [InternalOnly] // TODO: Implement internal network restriction if needed
public class InternalMLController : ControllerBase
{
    private readonly IClassificationService _classificationService;
    private readonly ILogger<InternalMLController> _logger;

    public InternalMLController(IClassificationService classificationService, ILogger<InternalMLController> logger)
    {
        _classificationService = classificationService;
        _logger = logger;
    }

    [HttpPost("classify")]
    public async Task<IActionResult> Classify([FromBody] ClassifyRequest request)
    {
        if (string.IsNullOrEmpty(request.FilePath))
        {
            return BadRequest("FilePath is required");
        }

        try
        {
            var result = await _classificationService.ClassifyFilenameAsync(request.FilePath);
            
            if (result.IsFailure)
            {
                _logger.LogError("ML Error: {Error}", result.Error);
                return StatusCode(500, $"ML Error: {result.Error}");
            }

            return Ok(new ClassifyResponse
            {
                Category = result.Value.PredictedCategory,
                Confidence = result.Value.Confidence
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error classifying file: {FilePath}", request.FilePath);
            return StatusCode(500, "Internal ML Verification Error");
        }
    }

    public class ClassifyRequest
    {
        public string FilePath { get; set; } = string.Empty;
    }

    public class ClassifyResponse
    {
        public string Category { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }
}
