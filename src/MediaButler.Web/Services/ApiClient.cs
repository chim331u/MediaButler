using MediaButler.Core.Entities;
using MediaButler.Core.Enums;
using System.Net.Http.Json;
using System.Text.Json;

namespace MediaButler.Web.Services;

/// <summary>
/// HTTP client implementation for MediaButler API communication.
/// Handles all API requests with proper error handling and JSON serialization.
/// </summary>
public class ApiClient : IApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        
        // Configure API base URL (will be configured per environment)
        _httpClient.BaseAddress = new Uri(_httpClient.BaseAddress?.ToString().TrimEnd('/') + "/api/");
    }

    public async Task<IEnumerable<TrackedFile>> GetFilesAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<IEnumerable<TrackedFile>>("files", _jsonOptions);
            return response ?? new List<TrackedFile>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching files: {ex.Message}");
            return new List<TrackedFile>();
        }
    }

    public async Task<IEnumerable<TrackedFile>> GetFilesByStatusAsync(FileStatus status)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<IEnumerable<TrackedFile>>($"files/status/{status}", _jsonOptions);
            return response ?? new List<TrackedFile>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching files by status: {ex.Message}");
            return new List<TrackedFile>();
        }
    }

    public async Task<TrackedFile?> GetFileAsync(string hash)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<TrackedFile>($"files/{hash}", _jsonOptions);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching file {hash}: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> UpdateFileAsync(string hash, object updateData)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"files/{hash}", updateData, _jsonOptions);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating file {hash}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteFileAsync(string hash)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"files/{hash}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting file {hash}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ConfirmFileAsync(string hash, string category)
    {
        try
        {
            var payload = new { category };
            var response = await _httpClient.PostAsJsonAsync($"files/{hash}/confirm", payload, _jsonOptions);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error confirming file {hash}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> MoveFileAsync(string hash)
    {
        try
        {
            var response = await _httpClient.PostAsync($"files/{hash}/move", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error moving file {hash}: {ex.Message}");
            return false;
        }
    }

    public async Task<IEnumerable<TrackedFile>> GetPendingFilesAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<IEnumerable<TrackedFile>>("files/pending", _jsonOptions);
            return response ?? new List<TrackedFile>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching pending files: {ex.Message}");
            return new List<TrackedFile>();
        }
    }

    public async Task<Dictionary<string, object>> GetStatsAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<Dictionary<string, object>>("stats", _jsonOptions);
            return response ?? new Dictionary<string, object>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching stats: {ex.Message}");
            return new Dictionary<string, object>();
        }
    }

    public async Task<Dictionary<string, int>> GetStatusCountsAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<Dictionary<string, int>>("stats/status-counts", _jsonOptions);
            return response ?? new Dictionary<string, int>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching status counts: {ex.Message}");
            return new Dictionary<string, int>();
        }
    }

    public async Task<Dictionary<string, string>> GetConfigAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<Dictionary<string, string>>("config", _jsonOptions);
            return response ?? new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching config: {ex.Message}");
            return new Dictionary<string, string>();
        }
    }

    public async Task<bool> UpdateConfigAsync(Dictionary<string, string> config)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync("config", config, _jsonOptions);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating config: {ex.Message}");
            return false;
        }
    }

    public async Task<Dictionary<string, object>> GetHealthAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<Dictionary<string, object>>("health", _jsonOptions);
            return response ?? new Dictionary<string, object>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching health: {ex.Message}");
            return new Dictionary<string, object> { { "status", "error" }, { "message", ex.Message } };
        }
    }
}