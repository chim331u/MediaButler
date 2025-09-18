using MediaButler.Web.Models;
using Microsoft.JSInterop;
using System.Text.Json;

namespace MediaButler.Web.Services.Search;

/// <summary>
/// Service implementation for managing saved searches using localStorage
/// </summary>
public class SavedSearchService : ISavedSearchService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<SavedSearchService> _logger;
    
    private const string SavedSearchesKey = "mediabutler_saved_searches";
    private const string RecentSearchesKey = "mediabutler_recent_searches";
    private const int MaxRecentSearches = 50;

    public SavedSearchService(IJSRuntime jsRuntime, ILogger<SavedSearchService> logger)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SavedSearchModel> SaveSearchAsync(string name, SearchCriteriaModel criteria, CancellationToken cancellationToken = default)
    {
        try
        {
            var savedSearches = await GetSavedSearchesFromStorageAsync();
            
            var savedSearch = new SavedSearchModel
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Criteria = criteria,
                CreatedDate = DateTime.UtcNow,
                LastUsedDate = DateTime.UtcNow,
                UseCount = 0,
                IsFavorite = false
            };

            savedSearches.Add(savedSearch);
            await SaveSearchesToStorageAsync(savedSearches);
            
            _logger.LogInformation("Saved search '{Name}' with ID {Id}", name, savedSearch.Id);
            return savedSearch;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving search '{Name}'", name);
            throw;
        }
    }

    public async Task<List<SavedSearchModel>> GetSavedSearchesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var savedSearches = await GetSavedSearchesFromStorageAsync();
            return savedSearches.OrderByDescending(s => s.LastUsedDate).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving saved searches");
            return new List<SavedSearchModel>();
        }
    }

    public async Task<SavedSearchModel?> GetSavedSearchAsync(string searchId, CancellationToken cancellationToken = default)
    {
        try
        {
            var savedSearches = await GetSavedSearchesFromStorageAsync();
            var search = savedSearches.FirstOrDefault(s => s.Id == searchId);
            
            if (search != null)
            {
                // Update last used date and use count
                var updatedSearch = search with 
                { 
                    LastUsedDate = DateTime.UtcNow, 
                    UseCount = search.UseCount + 1 
                };
                
                var index = savedSearches.FindIndex(s => s.Id == searchId);
                if (index >= 0)
                {
                    savedSearches[index] = updatedSearch;
                    await SaveSearchesToStorageAsync(savedSearches);
                }
                
                return updatedSearch;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving saved search {SearchId}", searchId);
            return null;
        }
    }

    public async Task<SavedSearchModel> UpdateSavedSearchAsync(string searchId, string name, SearchCriteriaModel criteria, CancellationToken cancellationToken = default)
    {
        try
        {
            var savedSearches = await GetSavedSearchesFromStorageAsync();
            var existingSearch = savedSearches.FirstOrDefault(s => s.Id == searchId);
            
            if (existingSearch == null)
                throw new InvalidOperationException($"Saved search with ID {searchId} not found");
            
            var updatedSearch = existingSearch with
            {
                Name = name,
                Criteria = criteria,
                LastUsedDate = DateTime.UtcNow
            };
            
            var index = savedSearches.FindIndex(s => s.Id == searchId);
            savedSearches[index] = updatedSearch;
            
            await SaveSearchesToStorageAsync(savedSearches);
            
            _logger.LogInformation("Updated saved search {SearchId}", searchId);
            return updatedSearch;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating saved search {SearchId}", searchId);
            throw;
        }
    }

    public async Task DeleteSavedSearchAsync(string searchId, CancellationToken cancellationToken = default)
    {
        try
        {
            var savedSearches = await GetSavedSearchesFromStorageAsync();
            var index = savedSearches.FindIndex(s => s.Id == searchId);
            
            if (index >= 0)
            {
                savedSearches.RemoveAt(index);
                await SaveSearchesToStorageAsync(savedSearches);
                _logger.LogInformation("Deleted saved search {SearchId}", searchId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting saved search {SearchId}", searchId);
            throw;
        }
    }

    public async Task<List<RecentSearchModel>> GetRecentSearchesAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            var recentSearches = await GetRecentSearchesFromStorageAsync();
            return recentSearches.OrderByDescending(s => s.SearchDate).Take(limit).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent searches");
            return new List<RecentSearchModel>();
        }
    }

    public async Task AddToRecentSearchesAsync(SearchCriteriaModel criteria, CancellationToken cancellationToken = default)
    {
        try
        {
            // Only add to recent searches if there are active criteria
            if (!criteria.HasActiveCriteria())
                return;

            var recentSearches = await GetRecentSearchesFromStorageAsync();
            
            // Check if this search already exists in recent searches
            var existingIndex = recentSearches.FindIndex(s => AreSearchCriteriaEqual(s.Criteria, criteria));
            if (existingIndex >= 0)
            {
                // Update the existing entry with new date
                var existing = recentSearches[existingIndex];
                recentSearches[existingIndex] = existing with { SearchDate = DateTime.UtcNow };
            }
            else
            {
                // Add new recent search
                var recentSearch = new RecentSearchModel
                {
                    Id = Guid.NewGuid().ToString(),
                    Criteria = criteria,
                    SearchDate = DateTime.UtcNow,
                    ResultsCount = 0 // Will be updated when actual search is performed
                };
                
                recentSearches.Insert(0, recentSearch);
            }
            
            // Keep only the most recent searches
            if (recentSearches.Count > MaxRecentSearches)
            {
                recentSearches = recentSearches.Take(MaxRecentSearches).ToList();
            }
            
            await SaveRecentSearchesToStorageAsync(recentSearches);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding to recent searches");
        }
    }

    public async Task ClearRecentSearchesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, RecentSearchesKey);
            _logger.LogInformation("Cleared recent searches");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing recent searches");
            throw;
        }
    }

    public async Task<List<SearchSuggestionModel>> GetSearchSuggestionsAsync(string query, int limit = 5, CancellationToken cancellationToken = default)
    {
        try
        {
            var suggestions = new List<SearchSuggestionModel>();
            
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return suggestions;

            var lowerQuery = query.ToLowerInvariant();
            
            // Get suggestions from saved searches
            var savedSearches = await GetSavedSearchesFromStorageAsync();
            var savedSuggestions = savedSearches
                .Where(s => s.Name.ToLowerInvariant().Contains(lowerQuery))
                .OrderByDescending(s => s.UseCount)
                .Take(2)
                .Select(s => new SearchSuggestionModel
                {
                    Text = s.Name,
                    Type = SearchSuggestionType.SavedSearch,
                    Category = "Saved Searches",
                    UsageCount = s.UseCount
                });
            
            suggestions.AddRange(savedSuggestions);
            
            // Get suggestions from recent searches
            var recentSearches = await GetRecentSearchesFromStorageAsync();
            var recentSuggestions = recentSearches
                .Where(s => !string.IsNullOrEmpty(s.Criteria.QuickSearch) && 
                           s.Criteria.QuickSearch.ToLowerInvariant().Contains(lowerQuery))
                .OrderByDescending(s => s.SearchDate)
                .Take(3)
                .Select(s => new SearchSuggestionModel
                {
                    Text = s.Criteria.QuickSearch,
                    Type = SearchSuggestionType.Query,
                    Category = "Recent Searches",
                    UsageCount = 0
                });
            
            suggestions.AddRange(recentSuggestions);
            
            // Add some common category suggestions
            var categories = new[] { "TV SERIES", "MOVIES", "DOCUMENTARIES", "ANIME", "MUSIC" };
            var categorySuggestions = categories
                .Where(c => c.ToLowerInvariant().Contains(lowerQuery))
                .Select(c => new SearchSuggestionModel
                {
                    Text = c,
                    Type = SearchSuggestionType.Category,
                    Category = "Categories",
                    UsageCount = 0
                });
            
            suggestions.AddRange(categorySuggestions);
            
            return suggestions.Take(limit).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting search suggestions for query '{Query}'", query);
            return new List<SearchSuggestionModel>();
        }
    }

    // Private helper methods
    private async Task<List<SavedSearchModel>> GetSavedSearchesFromStorageAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", SavedSearchesKey);
            if (string.IsNullOrEmpty(json))
                return new List<SavedSearchModel>();
            
            return JsonSerializer.Deserialize<List<SavedSearchModel>>(json) ?? new List<SavedSearchModel>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading saved searches from storage, returning empty list");
            return new List<SavedSearchModel>();
        }
    }

    private async Task SaveSearchesToStorageAsync(List<SavedSearchModel> savedSearches)
    {
        var json = JsonSerializer.Serialize(savedSearches);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", SavedSearchesKey, json);
    }

    private async Task<List<RecentSearchModel>> GetRecentSearchesFromStorageAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", RecentSearchesKey);
            if (string.IsNullOrEmpty(json))
                return new List<RecentSearchModel>();
            
            return JsonSerializer.Deserialize<List<RecentSearchModel>>(json) ?? new List<RecentSearchModel>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading recent searches from storage, returning empty list");
            return new List<RecentSearchModel>();
        }
    }

    private async Task SaveRecentSearchesToStorageAsync(List<RecentSearchModel> recentSearches)
    {
        var json = JsonSerializer.Serialize(recentSearches);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", RecentSearchesKey, json);
    }

    private static bool AreSearchCriteriaEqual(SearchCriteriaModel a, SearchCriteriaModel b)
    {
        // Compare the important fields that make searches equivalent
        return a.QuickSearch == b.QuickSearch &&
               a.Category == b.Category &&
               a.Status == b.Status &&
               a.FileExtension == b.FileExtension &&
               a.Quality == b.Quality &&
               a.MinSizeMB == b.MinSizeMB &&
               a.MaxSizeMB == b.MaxSizeMB &&
               a.FromDate == b.FromDate &&
               a.ToDate == b.ToDate &&
               a.MinConfidence == b.MinConfidence &&
               a.OnlyAutoClassified == b.OnlyAutoClassified &&
               a.OnlyManuallyReviewed == b.OnlyManuallyReviewed &&
               a.HasErrors == b.HasErrors;
    }
}