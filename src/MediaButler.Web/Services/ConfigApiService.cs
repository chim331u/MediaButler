using MediaButler.Web.Interfaces;
using MediaButler.Web.Models;

namespace MediaButler.Web.Services;

/// <summary>
/// Configuration API service following "Simple Made Easy" principles.
/// Single responsibility: Configuration CRUD operations only.
/// Composes with IHttpClientService without braiding concerns.
/// </summary>
public interface IConfigApiService
{
    /// <summary>
    /// Gets all configuration settings with optional filtering.
    /// Pure function - same inputs produce same outputs.
    /// </summary>
    Task<Result<IReadOnlyList<ConfigurationDto>>> GetAllSettingsAsync(
        ConfigurationFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets configuration settings for a specific section.
    /// </summary>
    Task<Result<IReadOnlyList<ConfigurationDto>>> GetSectionSettingsAsync(
        string section,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific configuration setting by key.
    /// </summary>
    Task<Result<ConfigurationDto>> GetSettingAsync(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new configuration setting.
    /// </summary>
    Task<Result<ConfigurationDto>> CreateSettingAsync(
        CreateConfigurationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing configuration setting.
    /// </summary>
    Task<Result<ConfigurationDto>> UpdateSettingAsync(
        string key,
        UpdateConfigurationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a configuration setting.
    /// </summary>
    Task<Result> DeleteSettingAsync(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets a setting to its default value.
    /// </summary>
    Task<Result<ConfigurationDto>> ResetSettingAsync(
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches configuration settings by pattern.
    /// </summary>
    Task<Result<IReadOnlyList<ConfigurationDto>>> SearchSettingsAsync(
        string pattern,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads watch folders configuration without restarting the service.
    /// </summary>
    Task<Result<ReloadWatchFoldersResponse>> ReloadWatchFoldersAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of configuration API service.
/// No state - each request is independent.
/// Values over exceptions - returns explicit Results.
/// </summary>
public class ConfigApiService : IConfigApiService
{
    private readonly IHttpClientService _httpClient;

    public ConfigApiService(IHttpClientService httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Result<IReadOnlyList<ConfigurationDto>>> GetAllSettingsAsync(
        ConfigurationFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use the new active endpoint to get all active configuration settings
            var result = await _httpClient.GetAsync<ConfigurationDto[]>("/api/config/active", cancellationToken);

            // Debug logging for troubleshooting
            Console.WriteLine($"[DEBUG] GetAllSettingsAsync - Success: {result.IsSuccess}, Count: {result.Value?.Length ?? 0}");

            if (!result.IsSuccess)
            {
                return Result<IReadOnlyList<ConfigurationDto>>.Failure(result.Error, result.StatusCode);
            }

            var settings = result.Value?.ToList() ?? new List<ConfigurationDto>();
            var filteredSettings = ApplyFilter(settings, filter);

            return Result<IReadOnlyList<ConfigurationDto>>.Success(filteredSettings);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<ConfigurationDto>>.Failure($"Failed to get settings: {ex.Message}");
        }
    }

    public async Task<Result<IReadOnlyList<ConfigurationDto>>> GetSectionSettingsAsync(
        string section,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _httpClient.GetAsync<ConfigurationDto[]>($"/api/config/sections/{section}", cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<IReadOnlyList<ConfigurationDto>>.Failure(result.Error, result.StatusCode);
            }

            var settings = result.Value?.ToList() ?? new List<ConfigurationDto>();
            return Result<IReadOnlyList<ConfigurationDto>>.Success(settings);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<ConfigurationDto>>.Failure($"Failed to get section settings: {ex.Message}");
        }
    }

    public async Task<Result<ConfigurationDto>> GetSettingAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _httpClient.GetAsync<ConfigurationDto>($"/api/config/settings/{key}", cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<ConfigurationDto>.Failure(result.Error, result.StatusCode);
            }

            return Result<ConfigurationDto>.Success(result.Value!);
        }
        catch (Exception ex)
        {
            return Result<ConfigurationDto>.Failure($"Failed to get setting: {ex.Message}");
        }
    }

    public async Task<Result<ConfigurationDto>> CreateSettingAsync(
        CreateConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _httpClient.PostAsync<ConfigurationDto>("/api/config/settings", request, cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<ConfigurationDto>.Failure(result.Error, result.StatusCode);
            }

            return Result<ConfigurationDto>.Success(result.Value!);
        }
        catch (Exception ex)
        {
            return Result<ConfigurationDto>.Failure($"Failed to create setting: {ex.Message}");
        }
    }

    public async Task<Result<ConfigurationDto>> UpdateSettingAsync(
        string key,
        UpdateConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _httpClient.PutAsync<ConfigurationDto>($"/api/config/settings/{key}", request, cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<ConfigurationDto>.Failure(result.Error, result.StatusCode);
            }

            return Result<ConfigurationDto>.Success(result.Value!);
        }
        catch (Exception ex)
        {
            return Result<ConfigurationDto>.Failure($"Failed to update setting: {ex.Message}");
        }
    }

    public async Task<Result> DeleteSettingAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _httpClient.DeleteAsync($"/api/config/settings/{key}", cancellationToken);

            if (!result.IsSuccess)
            {
                return Result.Failure(result.Error, result.StatusCode);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to delete setting: {ex.Message}");
        }
    }

    public async Task<Result<ConfigurationDto>> ResetSettingAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _httpClient.PostAsync<ConfigurationDto>($"/api/config/settings/{key}/reset", null, cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<ConfigurationDto>.Failure(result.Error, result.StatusCode);
            }

            return Result<ConfigurationDto>.Success(result.Value!);
        }
        catch (Exception ex)
        {
            return Result<ConfigurationDto>.Failure($"Failed to reset setting: {ex.Message}");
        }
    }

    public async Task<Result<IReadOnlyList<ConfigurationDto>>> SearchSettingsAsync(
        string pattern,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _httpClient.GetAsync<ConfigurationDto[]>($"/api/config/search?pattern={Uri.EscapeDataString(pattern)}", cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<IReadOnlyList<ConfigurationDto>>.Failure(result.Error, result.StatusCode);
            }

            var settings = result.Value?.ToList() ?? new List<ConfigurationDto>();
            return Result<IReadOnlyList<ConfigurationDto>>.Success(settings);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<ConfigurationDto>>.Failure($"Failed to search settings: {ex.Message}");
        }
    }

    public async Task<Result<ReloadWatchFoldersResponse>> ReloadWatchFoldersAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _httpClient.PostAsync<ReloadWatchFoldersResponse>("/api/config/reload-watch-folders", null, cancellationToken);

            if (!result.IsSuccess)
            {
                return Result<ReloadWatchFoldersResponse>.Failure(result.Error, result.StatusCode);
            }

            return Result<ReloadWatchFoldersResponse>.Success(result.Value!);
        }
        catch (Exception ex)
        {
            return Result<ReloadWatchFoldersResponse>.Failure($"Failed to reload watch folders: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies client-side filtering to configuration settings.
    /// Pure function - deterministic filtering logic.
    /// </summary>
    private static IReadOnlyList<ConfigurationDto> ApplyFilter(
        IList<ConfigurationDto> settings,
        ConfigurationFilter? filter)
    {
        if (filter == null)
            return settings.ToList();

        var filtered = settings.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter.SearchPattern))
        {
            filtered = filtered.Where(s =>
                s.Key.Contains(filter.SearchPattern, StringComparison.OrdinalIgnoreCase) ||
                (s.Description?.Contains(filter.SearchPattern, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(filter.Section))
        {
            filtered = filtered.Where(s =>
                s.Section.Equals(filter.Section, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.RequiresRestart.HasValue)
        {
            filtered = filtered.Where(s => s.RequiresRestart == filter.RequiresRestart.Value);
        }

        if (filter.IsModified.HasValue)
        {
            filtered = filtered.Where(s => s.IsModified == filter.IsModified.Value);
        }

        return filtered.ToList();
    }
}