using MediaButler.Core.Entities;
using MediaButler.Core.Enums;

namespace MediaButler.Web.Models;

/// <summary>
/// Model for file listing with pagination and sorting.
/// Following "Simple Made Easy" principles with immutable data structures.
/// </summary>
public record FileListModel
{
    public IEnumerable<TrackedFile> Files { get; init; } = Array.Empty<TrackedFile>();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string SortBy { get; init; } = "CreatedDate";
    public bool SortDescending { get; init; } = true;
    public string? SearchTerm { get; init; }
    public FileStatus? StatusFilter { get; init; }
    public string? CategoryFilter { get; init; }
    
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
    public int StartIndex => (PageNumber - 1) * PageSize + 1;
    public int EndIndex => Math.Min(PageNumber * PageSize, TotalCount);
}

public record FileListRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string SortBy { get; init; } = "CreatedDate";
    public bool SortDescending { get; init; } = true;
    public string? SearchTerm { get; init; }
    public FileStatus? StatusFilter { get; init; }
    public string? CategoryFilter { get; init; }
}

public static class FileSortOptions
{
    public static readonly Dictionary<string, string> Options = new()
    {
        ["FileName"] = "File Name",
        ["FileSize"] = "File Size",
        ["CreatedDate"] = "Date Added",
        ["LastUpdateDate"] = "Last Updated",
        ["Status"] = "Status",
        ["Category"] = "Category",
        ["Confidence"] = "Confidence"
    };
}