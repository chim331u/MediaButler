using MediaButler.Web.Interfaces;
using MediaButler.Web.Models;

namespace MediaButler.Web.Services;

/// <summary>
/// Example API service demonstrating usage of IHttpClientService.
/// Shows "Simple Made Easy" principles in action:
/// - Single responsibility: Only handles file-related API calls
/// - Compose don't complect: Uses HttpClientService without braiding logic
/// - Values over state: Returns immutable results
/// </summary>
public class ExampleApiService
{
    private readonly IHttpClientService _httpClient;

    public ExampleApiService(IHttpClientService httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Example: Get files from API
    /// Pure function - same inputs produce same outputs
    /// </summary>
    public async Task<Result<FileDto[]>> GetFilesAsync(CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetAsync<FileDto[]>("/api/files", cancellationToken);
    }

    /// <summary>
    /// Example: Move a file
    /// Pure function - explicit inputs and outputs
    /// </summary>
    public async Task<Result<MoveResult>> MoveFileAsync(string fileHash, string targetCategory, CancellationToken cancellationToken = default)
    {
        var payload = new { FileHash = fileHash, TargetCategory = targetCategory };
        return await _httpClient.PostAsync<MoveResult>("/api/files/move", payload, cancellationToken);
    }

    /// <summary>
    /// Example: Delete a file
    /// Pure function - deterministic result
    /// </summary>
    public async Task<Result> DeleteFileAsync(string fileHash, CancellationToken cancellationToken = default)
    {
        return await _httpClient.DeleteAsync($"/api/files/{fileHash}", cancellationToken);
    }
}

// Example DTOs following the same principles
public record FileDto(string Hash, string Name, string Category, DateTime CreatedDate);
public record MoveResult(bool Success, string? NewPath, string? Error);