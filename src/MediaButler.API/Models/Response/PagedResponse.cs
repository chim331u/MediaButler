using System.Text.Json.Serialization;

namespace MediaButler.API.Models.Response;

/// <summary>
/// Generic wrapper for paginated API responses.
/// Provides consistent pagination metadata and data structure following "Simple Made Easy" principles.
/// Separates pagination concerns from the actual data being returned.
/// </summary>
/// <typeparam name="T">The type of data being paginated.</typeparam>
/// <remarks>
/// This class ensures all paginated endpoints return the same structure, making client integration
/// predictable and simple. The pagination metadata is separated from the data items to avoid complecting
/// different concerns in the response structure.
/// </remarks>
public class PagedResponse<T>
{
    /// <summary>
    /// Gets or sets the collection of items for the current page.
    /// Contains the actual data being paginated.
    /// </summary>
    /// <value>A collection of items of type T for the current page.</value>
    public IReadOnlyCollection<T> Items { get; set; } = Array.Empty<T>();

    /// <summary>
    /// Gets or sets the current page number (1-based).
    /// Indicates which page of results is being returned.
    /// </summary>
    /// <value>The current page number starting from 1.</value>
    /// <example>1</example>
    public int Page { get; set; }

    /// <summary>
    /// Gets or sets the number of items per page.
    /// Indicates the maximum number of items that can be returned per page.
    /// </summary>
    /// <value>The page size limit.</value>
    /// <example>20</example>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the total number of items across all pages.
    /// Provides context for pagination controls and progress indicators.
    /// </summary>
    /// <value>The total count of items available.</value>
    /// <example>156</example>
    public int TotalItems { get; set; }

    /// <summary>
    /// Gets the total number of pages available.
    /// Calculated based on TotalItems and PageSize to provide pagination navigation context.
    /// </summary>
    /// <value>The total number of pages available.</value>
    /// <example>8</example>
    [JsonInclude]
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalItems / PageSize) : 0;

    /// <summary>
    /// Gets whether there is a previous page available.
    /// Used for enabling/disabling previous page navigation controls.
    /// </summary>
    /// <value>True if a previous page exists.</value>
    /// <example>false</example>
    [JsonInclude]
    public bool HasPreviousPage => Page > 1;

    /// <summary>
    /// Gets whether there is a next page available.
    /// Used for enabling/disabling next page navigation controls.
    /// </summary>
    /// <value>True if a next page exists.</value>
    /// <example>true</example>
    [JsonInclude]
    public bool HasNextPage => Page < TotalPages;

    /// <summary>
    /// Gets the number of items in the current page.
    /// Provides actual count of items returned (may be less than PageSize on last page).
    /// </summary>
    /// <value>The number of items in the current page.</value>
    /// <example>20</example>
    [JsonInclude]
    public int ItemCount => Items?.Count ?? 0;

    /// <summary>
    /// Gets the starting item number for the current page (1-based).
    /// Helps users understand their position in the overall dataset.
    /// </summary>
    /// <value>The 1-based index of the first item on the current page.</value>
    /// <example>21</example>
    [JsonInclude]
    public int StartItem => TotalItems > 0 ? ((Page - 1) * PageSize) + 1 : 0;

    /// <summary>
    /// Gets the ending item number for the current page (1-based).
    /// Helps users understand their position in the overall dataset.
    /// </summary>
    /// <value>The 1-based index of the last item on the current page.</value>
    /// <example>40</example>
    [JsonInclude]
    public int EndItem => Math.Min(StartItem + ItemCount - 1, TotalItems);

    /// <summary>
    /// Gets whether this page contains any items.
    /// Useful for conditional rendering in client applications.
    /// </summary>
    /// <value>True if the current page has items.</value>
    /// <example>true</example>
    [JsonInclude]
    public bool HasItems => ItemCount > 0;

    /// <summary>
    /// Gets whether this is the first page.
    /// Convenience property for client-side navigation logic.
    /// </summary>
    /// <value>True if this is page 1.</value>
    /// <example>true</example>
    [JsonInclude]
    public bool IsFirstPage => Page == 1;

    /// <summary>
    /// Gets whether this is the last page.
    /// Convenience property for client-side navigation logic.
    /// </summary>
    /// <value>True if this is the last page.</value>
    /// <example>false</example>
    [JsonInclude]
    public bool IsLastPage => Page == TotalPages;

    /// <summary>
    /// Initializes a new instance of the PagedResponse class.
    /// Default constructor for serialization support.
    /// </summary>
    public PagedResponse()
    {
    }

    /// <summary>
    /// Initializes a new instance of the PagedResponse class with pagination parameters.
    /// </summary>
    /// <param name="items">The items for the current page.</param>
    /// <param name="page">The current page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="totalItems">The total number of items across all pages.</param>
    public PagedResponse(IReadOnlyCollection<T> items, int page, int pageSize, int totalItems)
    {
        Items = items ?? throw new ArgumentNullException(nameof(items));
        Page = page;
        PageSize = pageSize;
        TotalItems = totalItems;
    }

    /// <summary>
    /// Creates a PagedResponse from a collection of items with pagination parameters.
    /// Factory method for creating paginated responses from data collections.
    /// </summary>
    /// <param name="items">The items for the current page.</param>
    /// <param name="page">The current page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="totalItems">The total number of items across all pages.</param>
    /// <returns>A new PagedResponse instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when items is null.</exception>
    /// <exception cref="ArgumentException">Thrown when page or pageSize are less than 1.</exception>
    public static PagedResponse<T> Create(IReadOnlyCollection<T> items, int page, int pageSize, int totalItems)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        
        if (page < 1)
            throw new ArgumentException("Page must be greater than 0", nameof(page));
        
        if (pageSize < 1)
            throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));
        
        if (totalItems < 0)
            throw new ArgumentException("Total items cannot be negative", nameof(totalItems));

        return new PagedResponse<T>(items, page, pageSize, totalItems);
    }

    /// <summary>
    /// Creates an empty PagedResponse.
    /// Useful for returning empty results with valid pagination metadata.
    /// </summary>
    /// <param name="page">The current page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <returns>A new empty PagedResponse instance.</returns>
    public static PagedResponse<T> Empty(int page = 1, int pageSize = 20)
    {
        return new PagedResponse<T>(Array.Empty<T>(), page, pageSize, 0);
    }

    /// <summary>
    /// Creates a single-page response containing all provided items.
    /// Useful when pagination is not needed but consistent response structure is desired.
    /// </summary>
    /// <param name="items">All items to include in the response.</param>
    /// <returns>A new PagedResponse containing all items on page 1.</returns>
    public static PagedResponse<T> Single(IReadOnlyCollection<T> items)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));

        var itemCount = items.Count;
        return new PagedResponse<T>(items, 1, Math.Max(itemCount, 1), itemCount);
    }
}

/// <summary>
/// Provides pagination parameters for API requests.
/// Separates pagination input concerns from response formatting.
/// </summary>
/// <remarks>
/// This class standardizes how pagination parameters are received from clients,
/// ensuring consistent behavior across all paginated endpoints.
/// </remarks>
public class PaginationRequest
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    /// <summary>
    /// Gets or sets the requested page number (1-based).
    /// Automatically constrained to be at least 1.
    /// </summary>
    /// <value>The requested page number starting from 1.</value>
    /// <example>1</example>
    public int Page
    {
        get => _page;
        set => _page = Math.Max(1, value);
    }

    /// <summary>
    /// Gets or sets the requested number of items per page.
    /// Automatically constrained between 1 and MaxPageSize (100).
    /// </summary>
    /// <value>The requested page size between 1 and 100.</value>
    /// <example>20</example>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Max(1, Math.Min(MaxPageSize, value));
    }

    /// <summary>
    /// Gets the zero-based offset for database queries.
    /// Calculated as (Page - 1) * PageSize for use with Skip() operations.
    /// </summary>
    /// <value>The zero-based offset for the current page.</value>
    /// <example>0</example>
    [JsonIgnore]
    public int Offset => (Page - 1) * PageSize;

    /// <summary>
    /// Validates the pagination request parameters.
    /// Ensures page and pageSize are within acceptable ranges.
    /// </summary>
    /// <returns>True if the pagination request is valid.</returns>
    public bool IsValid()
    {
        return Page >= 1 && PageSize >= 1 && PageSize <= MaxPageSize;
    }

    /// <summary>
    /// Creates a default pagination request.
    /// Returns page 1 with default page size of 20.
    /// </summary>
    /// <returns>A new PaginationRequest with default values.</returns>
    public static PaginationRequest Default()
    {
        return new PaginationRequest();
    }

    /// <summary>
    /// Creates a pagination request with specified parameters.
    /// Automatically applies constraints to ensure valid values.
    /// </summary>
    /// <param name="page">The desired page number (will be constrained to >= 1).</param>
    /// <param name="pageSize">The desired page size (will be constrained to 1-100).</param>
    /// <returns>A new PaginationRequest with the specified parameters.</returns>
    public static PaginationRequest Create(int page, int pageSize)
    {
        return new PaginationRequest { Page = page, PageSize = pageSize };
    }
}