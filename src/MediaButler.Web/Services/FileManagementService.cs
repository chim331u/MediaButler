using MediaButler.Core.Entities;
using MediaButler.Core.Enums;

namespace MediaButler.Web.Services;

/// <summary>
/// UI service implementation for file management operations.
/// Encapsulates business logic and API calls for file-related operations.
/// </summary>
public class FileManagementService : IFileManagementService
{
    private readonly IApiClient _apiClient;

    public FileManagementService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IEnumerable<TrackedFile>> GetAllFilesAsync()
    {
        return await _apiClient.GetFilesAsync();
    }

    public async Task<IEnumerable<TrackedFile>> GetFilesByStatusAsync(FileStatus status)
    {
        return await _apiClient.GetFilesByStatusAsync(status);
    }

    public async Task<IEnumerable<TrackedFile>> GetPendingReviewFilesAsync()
    {
        return await _apiClient.GetPendingFilesAsync();
    }

    public async Task<TrackedFile?> GetFileDetailsAsync(string hash)
    {
        return await _apiClient.GetFileAsync(hash);
    }

    public async Task<bool> ConfirmAndMoveFileAsync(string hash, string category)
    {
        // First confirm the file with the specified category
        var confirmResult = await _apiClient.ConfirmFileAsync(hash, category);
        if (!confirmResult)
        {
            return false;
        }

        // Then move the file
        return await _apiClient.MoveFileAsync(hash);
    }

    public async Task<bool> RejectFileAsync(string hash, string reason)
    {
        // Update file status to indicate rejection
        var updateData = new 
        { 
            status = FileStatus.Error,
            note = $"Rejected: {reason}"
        };
        
        return await _apiClient.UpdateFileAsync(hash, updateData);
    }

    public async Task<bool> RetryProcessingAsync(string hash)
    {
        // Reset file status to allow reprocessing
        var updateData = new 
        { 
            status = FileStatus.New,
            note = "Retry processing requested"
        };
        
        return await _apiClient.UpdateFileAsync(hash, updateData);
    }

    public async Task<bool> UpdateFileCategoryAsync(string hash, string category)
    {
        // Update file category and mark as ready to move
        var updateData = new
        {
            category = category,
            status = FileStatus.ReadyToMove,
            note = $"Category updated to: {category}"
        };

        return await _apiClient.UpdateFileAsync(hash, updateData);
    }

    public async Task<int> ConfirmMultipleFilesAsync(IEnumerable<string> hashes, string category)
    {
        var successCount = 0;
        var tasks = hashes.Select(async hash =>
        {
            try
            {
                var success = await ConfirmAndMoveFileAsync(hash, category);
                if (success)
                {
                    Interlocked.Increment(ref successCount);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error confirming file {hash}: {ex.Message}");
            }
        });

        await Task.WhenAll(tasks);
        return successCount;
    }

    public async Task<int> RejectMultipleFilesAsync(IEnumerable<string> hashes, string reason)
    {
        var successCount = 0;
        var tasks = hashes.Select(async hash =>
        {
            try
            {
                var success = await RejectFileAsync(hash, reason);
                if (success)
                {
                    Interlocked.Increment(ref successCount);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rejecting file {hash}: {ex.Message}");
            }
        });

        await Task.WhenAll(tasks);
        return successCount;
    }

    public async Task<FileStatistics> GetFileStatisticsAsync()
    {
        try
        {
            var stats = await _apiClient.GetStatsAsync();
            var statusCounts = await _apiClient.GetStatusCountsAsync();

            return new FileStatistics
            {
                TotalFiles = statusCounts.Values.Sum(),
                PendingFiles = statusCounts.GetValueOrDefault(nameof(FileStatus.Classified), 0),
                ProcessedFiles = statusCounts.GetValueOrDefault(nameof(FileStatus.Moved), 0),
                ErrorFiles = statusCounts.GetValueOrDefault(nameof(FileStatus.Error), 0),
                ProcessedToday = stats.TryGetValue("processedToday", out var todayValue) ? 
                    Convert.ToInt32(todayValue) : 0
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching statistics: {ex.Message}");
            return new FileStatistics();
        }
    }
}

/// <summary>
/// Statistics data model for UI display.
/// </summary>
public class FileStatistics
{
    public int TotalFiles { get; set; }
    public int PendingFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int ErrorFiles { get; set; }
    public int ProcessedToday { get; set; }
}