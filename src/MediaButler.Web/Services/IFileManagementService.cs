using MediaButler.Core.Entities;
using MediaButler.Core.Enums;

namespace MediaButler.Web.Services;

/// <summary>
/// UI service interface for file management operations.
/// Provides high-level business logic for file-related UI operations.
/// </summary>
public interface IFileManagementService
{
    // File retrieval
    Task<IEnumerable<TrackedFile>> GetAllFilesAsync();
    Task<IEnumerable<TrackedFile>> GetFilesByStatusAsync(FileStatus status);
    Task<IEnumerable<TrackedFile>> GetPendingReviewFilesAsync();
    Task<TrackedFile?> GetFileDetailsAsync(string hash);
    
    // File operations
    Task<bool> ConfirmAndMoveFileAsync(string hash, string category);
    Task<bool> RejectFileAsync(string hash, string reason);
    Task<bool> RetryProcessingAsync(string hash);
    Task<bool> UpdateFileCategoryAsync(string hash, string category);
    
    // Batch operations
    Task<int> ConfirmMultipleFilesAsync(IEnumerable<string> hashes, string category);
    Task<int> RejectMultipleFilesAsync(IEnumerable<string> hashes, string reason);
    
    // Statistics
    Task<FileStatistics> GetFileStatisticsAsync();
}