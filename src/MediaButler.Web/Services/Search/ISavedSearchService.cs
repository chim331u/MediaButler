using MediaButler.Web.Models;

namespace MediaButler.Web.Services.Search;

/// <summary>
/// Service for managing saved search queries and user search preferences
/// </summary>
public interface ISavedSearchService
{
    /// <summary>
    /// Saves a search query with a custom name
    /// </summary>
    /// <param name="name">Display name for the saved search</param>
    /// <param name="criteria">Search criteria to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The saved search with assigned ID</returns>
    Task<SavedSearchModel> SaveSearchAsync(string name, SearchCriteriaModel criteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all saved searches for the current user
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of saved searches</returns>
    Task<List<SavedSearchModel>> GetSavedSearchesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific saved search by ID
    /// </summary>
    /// <param name="searchId">Saved search ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Saved search or null if not found</returns>
    Task<SavedSearchModel?> GetSavedSearchAsync(string searchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing saved search
    /// </summary>
    /// <param name="searchId">Saved search ID</param>
    /// <param name="name">Updated name</param>
    /// <param name="criteria">Updated criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated saved search</returns>
    Task<SavedSearchModel> UpdateSavedSearchAsync(string searchId, string name, SearchCriteriaModel criteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a saved search
    /// </summary>
    /// <param name="searchId">Saved search ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteSavedSearchAsync(string searchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent searches (automatically saved search history)
    /// </summary>
    /// <param name="limit">Maximum number of recent searches to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of recent searches</returns>
    Task<List<RecentSearchModel>> GetRecentSearchesAsync(int limit = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a search to the recent searches history
    /// </summary>
    /// <param name="criteria">Search criteria to add to history</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddToRecentSearchesAsync(SearchCriteriaModel criteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the recent searches history
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ClearRecentSearchesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets search suggestions based on query input
    /// </summary>
    /// <param name="query">Partial query to get suggestions for</param>
    /// <param name="limit">Maximum number of suggestions</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of search suggestions</returns>
    Task<List<SearchSuggestionModel>> GetSearchSuggestionsAsync(string query, int limit = 5, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a saved search query
/// </summary>
public record SavedSearchModel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public SearchCriteriaModel Criteria { get; init; } = new();
    public DateTime CreatedDate { get; init; } = DateTime.UtcNow;
    public DateTime LastUsedDate { get; init; } = DateTime.UtcNow;
    public int UseCount { get; init; } = 0;
    public bool IsFavorite { get; init; } = false;
    
    public string Description => Criteria.GetDescription();
}

/// <summary>
/// Represents a recent search entry
/// </summary>
public record RecentSearchModel
{
    public string Id { get; init; } = string.Empty;
    public SearchCriteriaModel Criteria { get; init; } = new();
    public DateTime SearchDate { get; init; } = DateTime.UtcNow;
    public int ResultsCount { get; init; } = 0;
    
    public string Description => Criteria.GetDescription();
}

/// <summary>
/// Represents a search suggestion
/// </summary>
public record SearchSuggestionModel
{
    public string Text { get; init; } = string.Empty;
    public SearchSuggestionType Type { get; init; } = SearchSuggestionType.Query;
    public string Category { get; init; } = string.Empty;
    public int UsageCount { get; init; } = 0;
}

/// <summary>
/// Types of search suggestions
/// </summary>
public enum SearchSuggestionType
{
    Query,
    Category,
    Filename,
    Extension,
    SavedSearch
}