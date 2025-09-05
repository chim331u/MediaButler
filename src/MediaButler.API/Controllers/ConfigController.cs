using Microsoft.AspNetCore.Mvc;
using MediaButler.Services.Interfaces;
using MediaButler.API.Models.Response;
using MediaButler.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace MediaButler.API.Controllers;

/// <summary>
/// Provides configuration management endpoints for MediaButler settings.
/// Handles system configuration, user preferences, and path management.
/// Follows "Simple Made Easy" principles with clear configuration boundaries.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ConfigController : ControllerBase
{
    private readonly IConfigurationService _configurationService;

    /// <summary>
    /// Initializes a new instance of the ConfigController.
    /// </summary>
    /// <param name="configurationService">Service for configuration management</param>
    public ConfigController(IConfigurationService configurationService)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
    }

    /// <summary>
    /// Gets configuration settings for a specific section.
    /// </summary>
    /// <param name="section">Configuration section (e.g., "Paths", "ML", "Processing")</param>
    /// <returns>Configuration settings for the specified section</returns>
    /// <response code="200">Configuration settings retrieved successfully</response>
    /// <response code="400">Invalid section specified</response>
    /// <response code="404">Configuration section not found</response>
    [HttpGet("sections/{section}")]
    [ProducesResponseType(typeof(IEnumerable<ConfigurationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConfigurationSection([FromRoute] string section)
    {
        if (string.IsNullOrWhiteSpace(section))
        {
            return BadRequest(new { Error = "Section name cannot be empty." });
        }

        var result = await _configurationService.GetSectionAsync(section);
        
        if (!result.IsSuccess)
        {
            if (result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { Error = result.Error });
            }
            return BadRequest(new { Error = result.Error });
        }

        var responseSettings = result.Value.Select(s => s.ToResponse()).ToList();
        return Ok(responseSettings);
    }

    /// <summary>
    /// Gets a specific configuration setting by key.
    /// </summary>
    /// <param name="key">Configuration key (e.g., "Paths.WatchFolder", "ML.ConfidenceThreshold")</param>
    /// <returns>Configuration setting value and metadata</returns>
    /// <response code="200">Configuration setting retrieved successfully</response>
    /// <response code="400">Invalid configuration key</response>
    /// <response code="404">Configuration setting not found</response>
    [HttpGet("settings/{key}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConfigurationSetting([FromRoute] string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest(new { Error = "Configuration key cannot be empty." });
        }

        // For demo purposes, try to get as string first
        var result = await _configurationService.GetConfigurationAsync<string>(key);
        
        return result.IsSuccess 
            ? Ok(new { Key = key, Value = result.Value, Timestamp = DateTime.UtcNow })
            : NotFound(new { Error = result.Error });
    }

    /// <summary>
    /// Updates a configuration setting value.
    /// </summary>
    /// <param name="key">Configuration key to update</param>
    /// <param name="request">New configuration value and metadata</param>
    /// <returns>Updated configuration setting</returns>
    /// <response code="200">Configuration updated successfully</response>
    /// <response code="400">Invalid configuration key or value</response>
    /// <response code="404">Configuration setting not found</response>
    [HttpPut("settings/{key}")]
    [ProducesResponseType(typeof(ConfigurationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateConfigurationSetting(
        [FromRoute] string key, 
        [FromBody] UpdateConfigurationRequest request)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest(new { Error = "Configuration key cannot be empty." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _configurationService.SetConfigurationAsync(
            key, request.Value, request.Description, request.RequiresRestart);
        
        return result.IsSuccess 
            ? Ok(result.Value.ToResponse())
            : BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Creates a new configuration setting.
    /// </summary>
    /// <param name="request">Configuration setting to create</param>
    /// <returns>Created configuration setting</returns>
    /// <response code="201">Configuration setting created successfully</response>
    /// <response code="400">Invalid configuration setting</response>
    /// <response code="409">Configuration setting already exists</response>
    [HttpPost("settings")]
    [ProducesResponseType(typeof(ConfigurationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateConfigurationSetting([FromBody] CreateConfigurationRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Check if setting already exists
        var existsResult = await _configurationService.ConfigurationExistsAsync(request.Key);
        if (existsResult.IsSuccess && existsResult.Value)
        {
            return Conflict(new { Error = $"Configuration setting '{request.Key}' already exists." });
        }

        var result = await _configurationService.SetConfigurationAsync(
            request.Key, request.Value, request.Description, request.RequiresRestart);
        
        return result.IsSuccess 
            ? CreatedAtAction(
                nameof(GetConfigurationSetting), 
                new { key = request.Key }, 
                result.Value.ToResponse())
            : BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Deletes a configuration setting (removes it from the system).
    /// </summary>
    /// <param name="key">Configuration key to delete</param>
    /// <returns>No content on successful deletion</returns>
    /// <response code="204">Configuration setting deleted successfully</response>
    /// <response code="400">Invalid configuration key</response>
    /// <response code="404">Configuration setting not found</response>
    [HttpDelete("settings/{key}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteConfigurationSetting([FromRoute] string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest(new { Error = "Configuration key cannot be empty." });
        }

        var result = await _configurationService.RemoveConfigurationAsync(key);
        
        return result.IsSuccess 
            ? NoContent() 
            : NotFound(new { Error = result.Error });
    }

    /// <summary>
    /// Resets a configuration setting to its default value.
    /// </summary>
    /// <param name="key">Configuration key to reset</param>
    /// <returns>Reset configuration setting</returns>
    /// <response code="200">Configuration setting reset successfully</response>
    /// <response code="400">Invalid configuration key</response>
    /// <response code="404">Configuration setting not found</response>
    [HttpPost("settings/{key}/reset")]
    [ProducesResponseType(typeof(ConfigurationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetConfigurationSetting([FromRoute] string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest(new { Error = "Configuration key cannot be empty." });
        }

        var result = await _configurationService.ResetToDefaultAsync(key);
        
        return result.IsSuccess 
            ? Ok(result.Value.ToResponse())
            : NotFound(new { Error = result.Error });
    }

    /// <summary>
    /// Searches configuration settings by key pattern.
    /// </summary>
    /// <param name="pattern">Search pattern with wildcards (% for multiple chars, _ for single char)</param>
    /// <returns>Matching configuration settings</returns>
    /// <response code="200">Configuration settings found</response>
    /// <response code="400">Invalid search pattern</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<ConfigurationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchConfigurationSettings([FromQuery] string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return BadRequest(new { Error = "Search pattern cannot be empty." });
        }

        var result = await _configurationService.SearchConfigurationAsync(pattern);
        
        if (!result.IsSuccess)
        {
            return BadRequest(new { Error = result.Error });
        }

        var responseSettings = result.Value.Select(s => s.ToResponse()).ToList();
        return Ok(responseSettings);
    }

    /// <summary>
    /// Gets configuration settings that require application restart.
    /// </summary>
    /// <returns>Settings requiring restart</returns>
    /// <response code="200">Restart-required settings retrieved successfully</response>
    /// <response code="500">Failed to retrieve settings</response>
    [HttpGet("restart-required")]
    [ProducesResponseType(typeof(IEnumerable<ConfigurationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRestartRequiredSettings()
    {
        var result = await _configurationService.GetRestartRequiredSettingsAsync();
        
        if (!result.IsSuccess)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { Error = result.Error });
        }

        var responseSettings = result.Value.Select(s => s.ToResponse()).ToList();
        return Ok(responseSettings);
    }

    /// <summary>
    /// Gets recently modified configuration settings.
    /// </summary>
    /// <param name="hours">Number of hours to look back (default: 24)</param>
    /// <returns>Recently modified configuration settings</returns>
    /// <response code="200">Recent settings retrieved successfully</response>
    /// <response code="400">Invalid hours parameter</response>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(IEnumerable<ConfigurationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRecentlyModifiedSettings([FromQuery] int hours = 24)
    {
        if (hours < 1 || hours > 168) // Max 1 week
        {
            return BadRequest(new { Error = "Hours must be between 1 and 168 (1 week)." });
        }

        var result = await _configurationService.GetRecentlyModifiedAsync(hours);
        
        if (!result.IsSuccess)
        {
            return BadRequest(new { Error = result.Error });
        }

        var responseSettings = result.Value.Select(s => s.ToResponse()).ToList();
        return Ok(responseSettings);
    }

    /// <summary>
    /// Exports all configuration settings to JSON format.
    /// </summary>
    /// <returns>JSON representation of all configuration settings</returns>
    /// <response code="200">Configuration exported successfully</response>
    /// <response code="500">Failed to export configuration</response>
    [HttpGet("export")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportConfiguration()
    {
        var result = await _configurationService.ExportConfigurationAsync();
        
        return result.IsSuccess 
            ? Ok(new { Configuration = result.Value, ExportedAt = DateTime.UtcNow })
            : StatusCode(StatusCodes.Status500InternalServerError, new { Error = result.Error });
    }
}

/// <summary>
/// Request model for updating configuration settings.
/// </summary>
public class UpdateConfigurationRequest
{
    /// <summary>
    /// New configuration value.
    /// </summary>
    [Required(ErrorMessage = "Configuration value is required")]
    public required object Value { get; set; }

    /// <summary>
    /// Optional description of the setting.
    /// </summary>
    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters")]
    public string? Description { get; set; }

    /// <summary>
    /// Whether this setting requires application restart.
    /// </summary>
    public bool RequiresRestart { get; set; }
}

/// <summary>
/// Request model for creating new configuration settings.
/// </summary>
public class CreateConfigurationRequest
{
    /// <summary>
    /// Configuration key (e.g., "Paths.WatchFolder").
    /// </summary>
    [Required(ErrorMessage = "Configuration key is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Configuration key must be between 3 and 100 characters")]
    [RegularExpression(@"^[A-Za-z][A-Za-z0-9]*(\.[A-Za-z][A-Za-z0-9]*)*$", ErrorMessage = "Configuration key must be in format 'Section.Key' with alphanumeric characters")]
    public required string Key { get; set; }

    /// <summary>
    /// Configuration value.
    /// </summary>
    [Required(ErrorMessage = "Configuration value is required")]
    public required object Value { get; set; }

    /// <summary>
    /// Optional description of the setting.
    /// </summary>
    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters")]
    public string? Description { get; set; }

    /// <summary>
    /// Whether this setting requires application restart.
    /// </summary>
    public bool RequiresRestart { get; set; }
}