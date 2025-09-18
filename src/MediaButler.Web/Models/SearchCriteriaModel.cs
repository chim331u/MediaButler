namespace MediaButler.Web.Models;

/// <summary>
/// Comprehensive search criteria model for advanced file searching and filtering
/// </summary>
public record SearchCriteriaModel
{
    // Quick Search
    public string QuickSearch { get; set; } = string.Empty;

    // File Properties
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;

    // File Size (in MB)
    public int? MinSizeMB { get; set; }
    public int? MaxSizeMB { get; set; }

    // Date Range
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }

    // ML Classification
    public int MinConfidence { get; set; } = 0;
    public bool OnlyAutoClassified { get; set; } = false;
    public bool OnlyManuallyReviewed { get; set; } = false;
    public bool HasErrors { get; set; } = false;

    // Sorting and Pagination
    public string SortBy { get; set; } = "CreatedDate";
    public string SortDirection { get; set; } = "Desc";
    public int PageSize { get; set; } = 25;
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Gets the count of active filters (non-default values)
    /// </summary>
    public int GetActiveFiltersCount()
    {
        var count = 0;

        if (!string.IsNullOrEmpty(QuickSearch)) count++;
        if (!string.IsNullOrEmpty(Category)) count++;
        if (!string.IsNullOrEmpty(Status)) count++;
        if (!string.IsNullOrEmpty(FileExtension)) count++;
        if (!string.IsNullOrEmpty(Quality)) count++;
        if (MinSizeMB.HasValue) count++;
        if (MaxSizeMB.HasValue) count++;
        if (FromDate.HasValue) count++;
        if (ToDate.HasValue) count++;
        if (MinConfidence > 0) count++;
        if (OnlyAutoClassified) count++;
        if (OnlyManuallyReviewed) count++;
        if (HasErrors) count++;

        return count;
    }

    /// <summary>
    /// Checks if any search criteria are active
    /// </summary>
    public bool HasActiveCriteria() => GetActiveFiltersCount() > 0;

    /// <summary>
    /// Converts the search criteria to a query string for URL sharing
    /// </summary>
    public string ToQueryString()
    {
        var parameters = new List<string>();

        if (!string.IsNullOrEmpty(QuickSearch))
            parameters.Add($"q={Uri.EscapeDataString(QuickSearch)}");
        if (!string.IsNullOrEmpty(Category))
            parameters.Add($"category={Uri.EscapeDataString(Category)}");
        if (!string.IsNullOrEmpty(Status))
            parameters.Add($"status={Uri.EscapeDataString(Status)}");
        if (!string.IsNullOrEmpty(FileExtension))
            parameters.Add($"ext={Uri.EscapeDataString(FileExtension)}");
        if (!string.IsNullOrEmpty(Quality))
            parameters.Add($"quality={Uri.EscapeDataString(Quality)}");
        if (MinSizeMB.HasValue)
            parameters.Add($"minSize={MinSizeMB}");
        if (MaxSizeMB.HasValue)
            parameters.Add($"maxSize={MaxSizeMB}");
        if (FromDate.HasValue)
            parameters.Add($"from={FromDate.Value:yyyy-MM-dd}");
        if (ToDate.HasValue)
            parameters.Add($"to={ToDate.Value:yyyy-MM-dd}");
        if (MinConfidence > 0)
            parameters.Add($"confidence={MinConfidence}");
        if (OnlyAutoClassified)
            parameters.Add("autoOnly=true");
        if (OnlyManuallyReviewed)
            parameters.Add("manualOnly=true");
        if (HasErrors)
            parameters.Add("errors=true");
        if (SortBy != "CreatedDate")
            parameters.Add($"sort={Uri.EscapeDataString(SortBy)}");
        if (SortDirection != "Desc")
            parameters.Add($"dir={SortDirection}");
        if (PageSize != 25)
            parameters.Add($"size={PageSize}");
        if (PageNumber != 1)
            parameters.Add($"page={PageNumber}");

        return parameters.Any() ? "?" + string.Join("&", parameters) : string.Empty;
    }

    /// <summary>
    /// Creates a SearchCriteriaModel from a query string
    /// </summary>
    public static SearchCriteriaModel FromQueryString(string queryString)
    {
        var criteria = new SearchCriteriaModel();
        
        if (string.IsNullOrEmpty(queryString))
            return criteria;

        // Remove leading '?' if present
        if (queryString.StartsWith("?"))
            queryString = queryString[1..];

        var parameters = queryString.Split('&')
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => Uri.UnescapeDataString(parts[1]));

        if (parameters.TryGetValue("q", out var quickSearch))
            criteria.QuickSearch = quickSearch;
        if (parameters.TryGetValue("category", out var category))
            criteria.Category = category;
        if (parameters.TryGetValue("status", out var status))
            criteria.Status = status;
        if (parameters.TryGetValue("ext", out var extension))
            criteria.FileExtension = extension;
        if (parameters.TryGetValue("quality", out var quality))
            criteria.Quality = quality;
        if (parameters.TryGetValue("minSize", out var minSize) && int.TryParse(minSize, out var minSizeInt))
            criteria.MinSizeMB = minSizeInt;
        if (parameters.TryGetValue("maxSize", out var maxSize) && int.TryParse(maxSize, out var maxSizeInt))
            criteria.MaxSizeMB = maxSizeInt;
        if (parameters.TryGetValue("from", out var fromDate) && DateOnly.TryParse(fromDate, out var fromDateValue))
            criteria.FromDate = fromDateValue;
        if (parameters.TryGetValue("to", out var toDate) && DateOnly.TryParse(toDate, out var toDateValue))
            criteria.ToDate = toDateValue;
        if (parameters.TryGetValue("confidence", out var confidence) && int.TryParse(confidence, out var confidenceInt))
            criteria.MinConfidence = confidenceInt;
        if (parameters.TryGetValue("autoOnly", out var autoOnly))
            criteria.OnlyAutoClassified = autoOnly == "true";
        if (parameters.TryGetValue("manualOnly", out var manualOnly))
            criteria.OnlyManuallyReviewed = manualOnly == "true";
        if (parameters.TryGetValue("errors", out var errors))
            criteria.HasErrors = errors == "true";
        if (parameters.TryGetValue("sort", out var sortBy))
            criteria.SortBy = sortBy;
        if (parameters.TryGetValue("dir", out var sortDirection))
            criteria.SortDirection = sortDirection;
        if (parameters.TryGetValue("size", out var pageSize) && int.TryParse(pageSize, out var pageSizeInt))
            criteria.PageSize = pageSizeInt;
        if (parameters.TryGetValue("page", out var pageNumber) && int.TryParse(pageNumber, out var pageNumberInt))
            criteria.PageNumber = pageNumberInt;

        return criteria;
    }

    /// <summary>
    /// Gets a human-readable description of the search criteria
    /// </summary>
    public string GetDescription()
    {
        var descriptions = new List<string>();

        if (!string.IsNullOrEmpty(QuickSearch))
            descriptions.Add($"Text: \"{QuickSearch}\"");
        if (!string.IsNullOrEmpty(Category))
            descriptions.Add($"Category: {Category}");
        if (!string.IsNullOrEmpty(Status))
            descriptions.Add($"Status: {Status}");
        if (!string.IsNullOrEmpty(FileExtension))
            descriptions.Add($"Extension: {FileExtension}");
        if (!string.IsNullOrEmpty(Quality))
            descriptions.Add($"Quality: {Quality}");
        if (MinSizeMB.HasValue || MaxSizeMB.HasValue)
        {
            var sizeDesc = MinSizeMB.HasValue && MaxSizeMB.HasValue 
                ? $"Size: {MinSizeMB}-{MaxSizeMB} MB"
                : MinSizeMB.HasValue 
                    ? $"Size: ≥{MinSizeMB} MB"
                    : $"Size: ≤{MaxSizeMB} MB";
            descriptions.Add(sizeDesc);
        }
        if (FromDate.HasValue || ToDate.HasValue)
        {
            var dateDesc = FromDate.HasValue && ToDate.HasValue
                ? $"Date: {FromDate:yyyy-MM-dd} to {ToDate:yyyy-MM-dd}"
                : FromDate.HasValue
                    ? $"Date: ≥{FromDate:yyyy-MM-dd}"
                    : $"Date: ≤{ToDate:yyyy-MM-dd}";
            descriptions.Add(dateDesc);
        }
        if (MinConfidence > 0)
            descriptions.Add($"Confidence: ≥{MinConfidence}%");
        if (OnlyAutoClassified)
            descriptions.Add("Auto-classified only");
        if (OnlyManuallyReviewed)
            descriptions.Add("Manually reviewed only");
        if (HasErrors)
            descriptions.Add("Has errors");

        return descriptions.Any() ? string.Join(", ", descriptions) : "No filters applied";
    }
}