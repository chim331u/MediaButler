using MediaButler.Core.Enums;

namespace MediaButler.API.Models.Request;

/// <summary>
/// Request model for querying files by multiple status values with pagination.
/// Follows "Simple Made Easy" principles by separating validation from controller logic.
/// </summary>
public class GetFilesByStatusesRequest
{
    /// <summary>
    /// Number of files to skip for pagination (offset).
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Number of files to return (page size, max 100).
    /// </summary>
    public int Take { get; set; } = 20;

    /// <summary>
    /// Array of file status values to filter by.
    /// </summary>
    public string[] Statuses { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Optional category filter.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Optional search term for filename or category.
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Column to sort by (FileName, Category, Status, CreatedDate, LastUpdateDate).
    /// </summary>
    public string? OrderBy { get; set; }

    /// <summary>
    /// Sort direction (true for descending, false for ascending).
    /// </summary>
    public bool Descending { get; set; } = true;

    /// <summary>
    /// Parses the string status values into strongly-typed FileStatus enums.
    /// This property encapsulates the parsing logic, keeping it out of the controller.
    /// </summary>
    public IEnumerable<FileStatus> ParsedStatuses
    {
        get
        {
            var parsedList = new List<FileStatus>();
            foreach (var status in Statuses)
            {
                if (!string.IsNullOrWhiteSpace(status) &&
                    Enum.TryParse<FileStatus>(status, true, out var statusValue) &&
                    !parsedList.Contains(statusValue))
                {
                    parsedList.Add(statusValue);
                }
            }
            return parsedList;
        }
    }
}
