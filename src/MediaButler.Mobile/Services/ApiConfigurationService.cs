using MediaButler.Mobile.Models;
using System.Text.Json;

namespace MediaButler.Mobile.Services;

/// <summary>
/// Implementation of API configuration management using FileSystem.AppDataDirectory.
/// Stores configuration as JSON file with in-memory caching.
/// </summary>
public class ApiConfigurationService : IApiConfigurationService
{
    private const string ConfigFileName = "api_config.json";
    private readonly string _configFilePath;
    private ApiConfiguration? _cachedConfiguration;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public event EventHandler<ApiConfiguration>? ConfigurationChanged;

    public ApiConfigurationService()
    {
        _configFilePath = Path.Combine(FileSystem.AppDataDirectory, ConfigFileName);
    }

    public async Task<ApiConfiguration> LoadConfigurationAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(_configFilePath))
            {
                // First run - create default configuration
                var defaultConfig = ApiConfiguration.CreateDefault();
                await SaveConfigurationInternalAsync(defaultConfig);
                _cachedConfiguration = defaultConfig;
                return defaultConfig;
            }

            var json = await File.ReadAllTextAsync(_configFilePath);
            var config = JsonSerializer.Deserialize<ApiConfiguration>(json, GetJsonOptions());

            if (config == null)
            {
                throw new InvalidOperationException("Failed to deserialize configuration file");
            }

            _cachedConfiguration = config;
            return config;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Configuration file is corrupted: {ex.Message}", ex);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveConfigurationAsync(ApiConfiguration configuration)
    {
        await _fileLock.WaitAsync();
        try
        {
            await SaveConfigurationInternalAsync(configuration);
            _cachedConfiguration = configuration;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task ReloadConfigurationAsync()
    {
        var config = await LoadConfigurationAsync();
        ConfigurationChanged?.Invoke(this, config);
    }

    public async Task AddEndpointAsync(ApiEndpoint endpoint)
    {
        var config = await LoadConfigurationAsync();

        // Check for duplicate names
        if (config.ApiEndpoints.Any(e => e.Name.Equals(endpoint.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Endpoint with name '{endpoint.Name}' already exists");
        }

        var updatedEndpoints = config.ApiEndpoints.ToList();
        updatedEndpoints.Add(endpoint);

        var updatedConfig = config with { ApiEndpoints = updatedEndpoints };
        await SaveConfigurationAsync(updatedConfig);
    }

    public async Task RemoveEndpointAsync(string endpointName)
    {
        var config = await LoadConfigurationAsync();

        var updatedEndpoints = config.ApiEndpoints
            .Where(e => !e.Name.Equals(endpointName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (updatedEndpoints.Count == config.ApiEndpoints.Count)
        {
            throw new InvalidOperationException($"Endpoint with name '{endpointName}' not found");
        }

        var updatedConfig = config with { ApiEndpoints = updatedEndpoints };
        await SaveConfigurationAsync(updatedConfig);
    }

    public async Task UpdateEndpointAsync(string endpointName, ApiEndpoint updatedEndpoint)
    {
        var config = await LoadConfigurationAsync();

        var updatedEndpoints = config.ApiEndpoints.ToList();
        var index = updatedEndpoints.FindIndex(e => e.Name.Equals(endpointName, StringComparison.OrdinalIgnoreCase));

        if (index == -1)
        {
            throw new InvalidOperationException($"Endpoint with name '{endpointName}' not found");
        }

        updatedEndpoints[index] = updatedEndpoint;

        var updatedConfig = config with { ApiEndpoints = updatedEndpoints };
        await SaveConfigurationAsync(updatedConfig);
    }

    public ApiConfiguration GetCurrentConfiguration()
    {
        // Return cached configuration or create default if not loaded yet
        if (_cachedConfiguration == null)
        {
            // Auto-initialize with default configuration
            _cachedConfiguration = ApiConfiguration.CreateDefault();
        }

        return _cachedConfiguration;
    }

    public Task<bool> HasConfigurationAsync()
    {
        return Task.FromResult(File.Exists(_configFilePath));
    }

    private async Task SaveConfigurationInternalAsync(ApiConfiguration configuration)
    {
        var json = JsonSerializer.Serialize(configuration, GetJsonOptions());
        await File.WriteAllTextAsync(_configFilePath, json);
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}
