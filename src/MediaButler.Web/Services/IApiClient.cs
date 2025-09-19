using MediaButler.Core.Entities;
using MediaButler.Core.Enums;

namespace MediaButler.Web.Services;

/// <summary>
/// HTTP client interface for MediaButler API communication.
/// Provides strongly-typed methods for all API endpoints.
/// </summary>
public interface IApiClient
{
    // File Management
    Task<IEnumerable<TrackedFile>> GetFilesAsync();
    Task<IEnumerable<TrackedFile>> GetFilesByStatusAsync(FileStatus status);
    Task<TrackedFile?> GetFileAsync(string hash);
    Task<bool> UpdateFileAsync(string hash, object updateData);
    Task<bool> DeleteFileAsync(string hash);
    
    // File Operations
    Task<bool> ConfirmFileAsync(string hash, string category);
    Task<bool> MoveFileAsync(string hash);
    Task<IEnumerable<TrackedFile>> GetPendingFilesAsync();
    
    // Statistics
    Task<Dictionary<string, object>> GetStatsAsync();
    Task<Dictionary<string, int>> GetStatusCountsAsync();
    
    // Configuration
    Task<Dictionary<string, string>> GetConfigAsync();
    Task<bool> UpdateConfigAsync(Dictionary<string, string> config);
    
    // Health
    Task<Dictionary<string, object>> GetHealthAsync();
    
    // Generic HTTP methods
    Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent? content, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> PostAsJsonAsync<T>(string requestUri, T value, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> PutAsJsonAsync<T>(string requestUri, T value, CancellationToken cancellationToken = default);
    Task<HttpResponseMessage> DeleteAsync(string requestUri, CancellationToken cancellationToken = default);
}