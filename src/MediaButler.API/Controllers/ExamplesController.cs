using Microsoft.AspNetCore.Mvc;
using MediaButler.Core.Models.Requests;

namespace MediaButler.API.Controllers;

/// <summary>
/// Controller providing example requests and documentation for the MediaButler API.
/// Useful for testing and understanding the batch file processing capabilities.
/// </summary>
[ApiController]
[Route("api/examples")]
[Produces("application/json")]
public class ExamplesController : ControllerBase
{
    /// <summary>
    /// Gets example batch organize requests for testing and documentation.
    /// Provides sample requests that can be used to test the batch file processing API.
    /// </summary>
    /// <returns>Collection of example batch organize requests</returns>
    /// <response code="200">Example requests retrieved successfully</response>
    [HttpGet("batch-organize-requests")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetBatchOrganizeExamples()
    {
        var examples = new
        {
            SimpleExample = new
            {
                Description = "Basic batch operation with 2 files",
                Request = new BatchOrganizeRequest
                {
                    Files = new List<FileActionDto>
                    {
                        new FileActionDto
                        {
                            Hash = "a1b2c3d4e5f6789012345678901234567890abcdef1234567890abcdef123456",
                            ConfirmedCategory = "BREAKING BAD"
                        },
                        new FileActionDto
                        {
                            Hash = "b2c3d4e5f6789012345678901234567890abcdef1234567890abcdef1234567",
                            ConfirmedCategory = "THE OFFICE"
                        }
                    },
                    BatchName = "Example Batch",
                    ContinueOnError = true,
                    DryRun = false
                }
            },
            DryRunExample = new
            {
                Description = "Dry run to validate operations without moving files",
                Request = new BatchOrganizeRequest
                {
                    Files = new List<FileActionDto>
                    {
                        new FileActionDto
                        {
                            Hash = "c3d4e5f6789012345678901234567890abcdef1234567890abcdef12345678",
                            ConfirmedCategory = "GAME OF THRONES"
                        }
                    },
                    BatchName = "Validation Test",
                    ContinueOnError = false,
                    DryRun = true,
                    ValidateTargetPaths = true
                }
            },
            CustomPathExample = new
            {
                Description = "Batch operation with custom target paths",
                Request = new BatchOrganizeRequest
                {
                    Files = new List<FileActionDto>
                    {
                        new FileActionDto
                        {
                            Hash = "d4e5f6789012345678901234567890abcdef1234567890abcdef123456789",
                            ConfirmedCategory = "DOCUMENTARIES",
                            CustomTargetPath = "/custom/path/documentaries/special_episode.mkv"
                        }
                    },
                    BatchName = "Custom Paths Batch",
                    CreateDirectories = true,
                    MaxConcurrency = 2
                }
            },
            LargeBatchExample = new
            {
                Description = "Large batch with error handling and monitoring",
                Request = new BatchOrganizeRequest
                {
                    Files = Enumerable.Range(1, 50).Select(i => new FileActionDto
                    {
                        Hash = $"hash{i:D2}".PadRight(64, '0'),
                        ConfirmedCategory = i % 3 == 0 ? "SERIES A" : i % 3 == 1 ? "SERIES B" : "SERIES C"
                    }).ToList(),
                    BatchName = "Large Test Batch",
                    ContinueOnError = true,
                    ValidateTargetPaths = true,
                    CreateDirectories = true,
                    MaxConcurrency = 3
                }
            }
        };

        return Ok(examples);
    }

    /// <summary>
    /// Gets example API usage scenarios and workflow descriptions.
    /// Provides documentation on how to use the batch file processing endpoints.
    /// </summary>
    /// <returns>API usage examples and workflows</returns>
    /// <response code="200">Usage examples retrieved successfully</response>
    [HttpGet("api-usage")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetApiUsageExamples()
    {
        var usage = new
        {
            BasicWorkflow = new
            {
                Description = "Standard batch file organization workflow",
                Steps = new[]
                {
                    "1. POST /api/v1/file-actions/organize-batch - Submit batch request",
                    "2. Receive job ID and initial status",
                    "3. Connect to SignalR hub at /file-processing",
                    "4. Join batch job group using jobId",
                    "5. Monitor real-time progress notifications",
                    "6. GET /api/v1/file-actions/batch-status/{jobId} - Check final status"
                },
                ExampleUrls = new
                {
                    SubmitBatch = "/api/v1/file-actions/organize-batch",
                    CheckStatus = "/api/v1/file-actions/batch-status/{jobId}",
                    ListJobs = "/api/v1/file-actions/batch-jobs",
                    CancelJob = "/api/v1/file-actions/batch-cancel/{jobId}",
                    SignalRHub = "/file-processing",
                    QueueMonitoring = "/api/v1/file-actions/batch-jobs (custom queue)"
                }
            },
            ValidationWorkflow = new
            {
                Description = "Pre-validate batch operations before execution",
                Steps = new[]
                {
                    "1. POST /api/v1/file-actions/validate-batch - Validate request",
                    "2. Review validation results and recommendations",
                    "3. Optionally run with DryRun=true first",
                    "4. Submit actual batch request if validation passes"
                }
            },
            MonitoringWorkflow = new
            {
                Description = "Real-time monitoring of batch operations",
                SignalREvents = new
                {
                    Connection = new[]
                    {
                        "ConnectionEstablished - Confirms hub connection",
                        "BatchJobJoined - Confirms subscription to job notifications"
                    },
                    BatchProgress = new[]
                    {
                        "BatchJobStarted - Job begins processing",
                        "BatchJobProgress - Progress updates with counts",
                        "BatchJobCompleted - Job finished successfully",
                        "BatchJobFailed - Job failed with error details"
                    },
                    FileProgress = new[]
                    {
                        "FileProcessingStarted - Individual file processing begins",
                        "FileOperationProgress - Detailed operation progress",
                        "FileProcessingCompleted - Individual file completed"
                    },
                    SystemNotifications = new[]
                    {
                        "SystemStatusUpdate - System component status",
                        "ProcessingError - Error notifications"
                    }
                }
            },
            ErrorHandling = new
            {
                Description = "Error handling and recovery strategies",
                Strategies = new
                {
                    ContinueOnError = "Set to true to process remaining files despite individual failures",
                    Validation = "Use validate-batch endpoint to check for issues before processing",
                    DryRun = "Use DryRun=true to test operations without actual file movement",
                    Monitoring = "Subscribe to SignalR notifications for real-time error alerts",
                    Cancellation = "Use batch-cancel endpoint to stop running jobs",
                    Retry = "Check failed files and resubmit them in a new batch"
                }
            },
            PerformanceOptimization = new
            {
                Description = "Tips for optimal batch processing performance",
                Recommendations = new[]
                {
                    "Keep batch sizes under 100 files for best performance",
                    "Use MaxConcurrency parameter to control resource usage",
                    "Enable DryRun for large batches to validate first",
                    "Monitor memory usage on ARM32 systems",
                    "Use ContinueOnError=true for resilient processing",
                    "Check batch job status via API endpoints for queue monitoring"
                }
            }
        };

        return Ok(usage);
    }

    /// <summary>
    /// Provides information about the current system configuration and capabilities.
    /// Useful for understanding system limits and optimization settings.
    /// </summary>
    /// <returns>System configuration information</returns>
    /// <response code="200">System information retrieved successfully</response>
    [HttpGet("system-info")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetSystemInfo()
    {
        var systemInfo = new
        {
            BatchProcessing = new
            {
                MaxFilesPerBatch = 1000,
                RecommendedBatchSize = 100,
                MaxConcurrency = Math.Max(1, Math.Min(Environment.ProcessorCount, 4)),
                SupportedFileExtensions = new[] { ".mkv", ".mp4", ".avi", ".m4v", ".wmv" },
                ARM32Optimized = true
            },
            SignalRConnections = new
            {
                FileProcessingHub = "/file-processing",
                NotificationHub = "/notifications",
                SupportedGroups = new[] { "batch-{jobId}", "file-processing" }
            },
            Validation = new
            {
                HashFormat = "SHA256 (64 character hexadecimal)",
                CategoryMaxLength = 100,
                CustomPathMaxLength = 500,
                MetadataMaxEntries = 20,
                MetadataValueMaxSize = 1000
            },
            Monitoring = new
            {
                BackgroundQueue = "/api/v1/file-actions/batch-status (custom queue)",
                SwaggerDocumentation = "/swagger",
                HealthCheck = "/api/health"
            },
            SystemConstraints = new
            {
                PlatformOptimization = "ARM32 NAS deployment",
                TargetMemoryFootprint = "< 300MB",
                MaxProcessingRate = "< 50 files/minute",
                DatabaseEngine = "SQLite",
                BackgroundJobEngine = "Custom Lightweight Queue"
            }
        };

        return Ok(systemInfo);
    }
}