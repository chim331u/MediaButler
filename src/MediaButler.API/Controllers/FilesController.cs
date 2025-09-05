using Microsoft.AspNetCore.Mvc;
using MediaButler.Services.Interfaces;
using MediaButler.API.Models.Response;
using MediaButler.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace MediaButler.API.Controllers;

/// <summary>
/// Provides file management endpoints for tracked files in MediaButler.
/// Handles CRUD operations, scanning, classification, and file movement.
/// Follows "Simple Made Easy" principles with clear REST semantics.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class FilesController : ControllerBase
{
    private readonly IFileService _fileService;

    /// <summary>
    /// Initializes a new instance of the FilesController.
    /// </summary>
    /// <param name="fileService">Service for file management operations</param>
    public FilesController(IFileService fileService)
    {
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
    }

    /// <summary>
    /// Gets tracked files with pagination and optional filtering by status.
    /// </summary>
    /// <param name="skip">Number of files to skip for pagination</param>
    /// <param name="take">Number of files to take (page size, max 100)</param>
    /// <param name="status">Optional file status filter</param>
    /// <param name="category">Optional category filter</param>
    /// <returns>List of tracked files</returns>
    /// <response code="200">Files retrieved successfully</response>
    /// <response code="400">Invalid pagination or filter parameters</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TrackedFileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFiles(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? category = null)
    {
        if (skip < 0 || take < 1 || take > 100)
        {
            return BadRequest(new { Error = "Invalid pagination parameters. Skip must be >= 0 and take must be between 1 and 100." });
        }

        FileStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<FileStatus>(status, true, out var statusValue))
            {
                return BadRequest(new { Error = $"Invalid status value: {status}" });
            }
            parsedStatus = statusValue;
        }

        var result = await _fileService.GetFilesPagedAsync(skip, take, parsedStatus, category);

        if (!result.IsSuccess)
        {
            return BadRequest(new { Error = result.Error });
        }

        var responseFiles = result.Value.Select(f => f.ToResponse()).ToList();
        return Ok(responseFiles);
    }

    /// <summary>
    /// Gets a specific tracked file by its hash.
    /// </summary>
    /// <param name="hash">SHA256 hash of the file</param>
    /// <returns>File details if found</returns>
    /// <response code="200">File found and returned</response>
    /// <response code="400">Invalid hash format</response>
    /// <response code="404">File not found</response>
    [HttpGet("{hash}")]
    [ProducesResponseType(typeof(TrackedFileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFile([FromRoute] string hash)
    {
        if (string.IsNullOrWhiteSpace(hash) || hash.Length != 64)
        {
            return BadRequest(new { Error = "Hash must be a valid 64-character SHA256 hash." });
        }

        var result = await _fileService.GetFileByHashAsync(hash);
        
        return result.IsSuccess 
            ? Ok(result.Value.ToResponse()) 
            : NotFound(new { Error = result.Error });
    }

    /// <summary>
    /// Registers a new file for tracking.
    /// </summary>
    /// <param name="request">File information to track</param>
    /// <returns>Created file information</returns>
    /// <response code="201">File added for tracking successfully</response>
    /// <response code="400">Invalid file information</response>
    /// <response code="409">File already exists</response>
    [HttpPost]
    [ProducesResponseType(typeof(TrackedFileResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddFile([FromBody] AddFileRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (!System.IO.File.Exists(request.FilePath))
        {
            return BadRequest(new { Error = "File does not exist at the specified path." });
        }

        var result = await _fileService.RegisterFileAsync(request.FilePath);

        if (!result.IsSuccess)
        {
            if (result.Error.Contains("already", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { Error = result.Error });
            }
            return BadRequest(new { Error = result.Error });
        }

        var response = result.Value.ToResponse();
        return CreatedAtAction(nameof(GetFile), new { hash = response.Hash }, response);
    }

    /// <summary>
    /// Gets files that are awaiting user confirmation after classification.
    /// </summary>
    /// <returns>List of files awaiting confirmation</returns>
    /// <response code="200">Pending files retrieved successfully</response>
    /// <response code="500">Failed to retrieve pending files</response>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(IEnumerable<TrackedFileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPendingFiles()
    {
        var result = await _fileService.GetFilesAwaitingConfirmationAsync();

        if (!result.IsSuccess)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { Error = result.Error });
        }

        var responseFiles = result.Value.Select(f => f.ToResponse()).ToList();
        return Ok(responseFiles);
    }

    /// <summary>
    /// Gets files ready for ML classification processing.
    /// </summary>
    /// <param name="limit">Maximum number of files to return (default: 50)</param>
    /// <returns>List of files ready for classification</returns>
    /// <response code="200">Files retrieved successfully</response>
    /// <response code="400">Invalid limit parameter</response>
    [HttpGet("ready-for-classification")]
    [ProducesResponseType(typeof(IEnumerable<TrackedFileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFilesReadyForClassification([FromQuery] int limit = 50)
    {
        if (limit < 1 || limit > 500)
        {
            return BadRequest(new { Error = "Limit must be between 1 and 500." });
        }

        var result = await _fileService.GetFilesReadyForClassificationAsync(limit);

        if (!result.IsSuccess)
        {
            return BadRequest(new { Error = result.Error });
        }

        var responseFiles = result.Value.Select(f => f.ToResponse()).ToList();
        return Ok(responseFiles);
    }

    /// <summary>
    /// Confirms a file's category assignment and marks it ready for processing.
    /// </summary>
    /// <param name="hash">SHA256 hash of the file</param>
    /// <param name="request">Category confirmation request</param>
    /// <returns>Updated file information</returns>
    /// <response code="200">File category confirmed successfully</response>
    /// <response code="400">Invalid confirmation request</response>
    /// <response code="404">File not found</response>
    [HttpPost("{hash}/confirm")]
    [ProducesResponseType(typeof(TrackedFileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmFileCategory(
        [FromRoute] string hash, 
        [FromBody] ConfirmCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(hash) || hash.Length != 64)
        {
            return BadRequest(new { Error = "Hash must be a valid 64-character SHA256 hash." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _fileService.ConfirmCategoryAsync(hash, request.Category);

        return result.IsSuccess 
            ? Ok(result.Value.ToResponse())
            : NotFound(new { Error = result.Error });
    }

    /// <summary>
    /// Marks a file as moved to its target location.
    /// </summary>
    /// <param name="hash">SHA256 hash of the file</param>
    /// <param name="request">Move confirmation request</param>
    /// <returns>Updated file information</returns>
    /// <response code="200">File marked as moved successfully</response>
    /// <response code="400">Invalid move request</response>
    /// <response code="404">File not found</response>
    [HttpPost("{hash}/moved")]
    [ProducesResponseType(typeof(TrackedFileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkFileAsMoved(
        [FromRoute] string hash, 
        [FromBody] MarkMovedRequest request)
    {
        if (string.IsNullOrWhiteSpace(hash) || hash.Length != 64)
        {
            return BadRequest(new { Error = "Hash must be a valid 64-character SHA256 hash." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _fileService.MarkFileAsMovedAsync(hash, request.TargetPath);

        return result.IsSuccess 
            ? Ok(result.Value.ToResponse())
            : NotFound(new { Error = result.Error });
    }

    /// <summary>
    /// Soft deletes a tracked file (sets IsActive to false).
    /// </summary>
    /// <param name="hash">SHA256 hash of the file</param>
    /// <param name="request">Optional deletion reason</param>
    /// <returns>No content on successful deletion</returns>
    /// <response code="204">File deleted successfully</response>
    /// <response code="400">Invalid hash format</response>
    /// <response code="404">File not found</response>
    [HttpDelete("{hash}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFile(
        [FromRoute] string hash,
        [FromBody] DeleteFileRequest? request = null)
    {
        if (string.IsNullOrWhiteSpace(hash) || hash.Length != 64)
        {
            return BadRequest(new { Error = "Hash must be a valid 64-character SHA256 hash." });
        }

        var result = await _fileService.DeleteFileAsync(hash, request?.Reason);
        
        return result.IsSuccess 
            ? NoContent() 
            : NotFound(new { Error = result.Error });
    }
}

/// <summary>
/// Request model for adding a new tracked file.
/// </summary>
public class AddFileRequest
{
    /// <summary>
    /// Full path to the file to track.
    /// </summary>
    [Required(ErrorMessage = "File path is required")]
    [StringLength(500, ErrorMessage = "File path must not exceed 500 characters")]
    public required string FilePath { get; set; }
}

/// <summary>
/// Request model for confirming file category.
/// </summary>
public class ConfirmCategoryRequest
{
    /// <summary>
    /// Confirmed category for the file.
    /// </summary>
    [Required(ErrorMessage = "Category is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Category must be between 1 and 100 characters")]
    [RegularExpression(@"^[A-Z0-9\s\-_\.]+$", ErrorMessage = "Category can only contain uppercase letters, numbers, spaces, hyphens, underscores, and dots")]
    public required string Category { get; set; }
}

/// <summary>
/// Request model for marking a file as moved.
/// </summary>
public class MarkMovedRequest
{
    /// <summary>
    /// Target path where the file was moved.
    /// </summary>
    [Required(ErrorMessage = "Target path is required")]
    [StringLength(500, ErrorMessage = "Target path must not exceed 500 characters")]
    public required string TargetPath { get; set; }
}

/// <summary>
/// Request model for deleting a file.
/// </summary>
public class DeleteFileRequest
{
    /// <summary>
    /// Optional reason for deleting the file.
    /// </summary>
    [StringLength(200, ErrorMessage = "Reason must not exceed 200 characters")]
    public string? Reason { get; set; }
}