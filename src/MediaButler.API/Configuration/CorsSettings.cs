using System.ComponentModel.DataAnnotations;

namespace MediaButler.API.Configuration;

/// <summary>
/// Configuration settings for CORS policy.
/// Supports flexible origin patterns including subnet wildcards for local network access.
/// </summary>
public class CorsSettings
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Cors";

    /// <summary>
    /// List of allowed origins for CORS requests.
    /// Supports wildcards for subnet matching (e.g., "192.168.1.*").
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one allowed origin must be configured")]
    public List<string> AllowedOrigins { get; set; } = new();

    /// <summary>
    /// Whether to allow wildcard subdomain matching.
    /// When true, origins like "192.168.1.*" will match any IP in that subnet.
    /// </summary>
    public bool AllowWildcardSubdomains { get; set; } = true;

    /// <summary>
    /// Whether to allow credentials in CORS requests.
    /// Required for SignalR functionality.
    /// </summary>
    public bool AllowCredentials { get; set; } = true;

    /// <summary>
    /// List of allowed HTTP methods for CORS requests.
    /// Empty list means allow all methods.
    /// </summary>
    public List<string> AllowedMethods { get; set; } = new();

    /// <summary>
    /// List of allowed headers for CORS requests.
    /// Empty list means allow all headers.
    /// </summary>
    public List<string> AllowedHeaders { get; set; } = new();

    /// <summary>
    /// Validates the CORS configuration.
    /// </summary>
    /// <returns>Collection of validation errors, if any</returns>
    public IEnumerable<ValidationResult> Validate()
    {
        var context = new ValidationContext(this);
        var results = new List<ValidationResult>();

        // Perform built-in validation
        Validator.TryValidateObject(this, context, results, validateAllProperties: true);

        // Custom validation logic
        foreach (var origin in AllowedOrigins)
        {
            if (string.IsNullOrWhiteSpace(origin))
            {
                results.Add(new ValidationResult("Origin cannot be null or empty", new[] { nameof(AllowedOrigins) }));
                continue;
            }

            // Validate wildcard patterns
            if (AllowWildcardSubdomains && origin.Contains('*'))
            {
                if (!IsValidWildcardPattern(origin))
                {
                    results.Add(new ValidationResult($"Invalid wildcard pattern in origin: {origin}", new[] { nameof(AllowedOrigins) }));
                }
            }
            else if (!IsValidOriginUrl(origin))
            {
                results.Add(new ValidationResult($"Invalid origin URL: {origin}", new[] { nameof(AllowedOrigins) }));
            }
        }

        return results;
    }

    /// <summary>
    /// Checks if an origin matches the configured allowed origins, including wildcard patterns.
    /// </summary>
    /// <param name="origin">The origin to check</param>
    /// <returns>True if the origin is allowed</returns>
    public bool IsOriginAllowed(string origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return false;

        foreach (var allowedOrigin in AllowedOrigins)
        {
            if (string.Equals(allowedOrigin, origin, StringComparison.OrdinalIgnoreCase))
                return true;

            if (AllowWildcardSubdomains && MatchesWildcardPattern(allowedOrigin, origin))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Validates if a wildcard pattern is properly formatted.
    /// </summary>
    private static bool IsValidWildcardPattern(string pattern)
    {
        // Simple validation for patterns like "http://192.168.1.*:*" or "https://192.168.1.*"
        if (pattern.Contains('*'))
        {
            try
            {
                var testPattern = pattern.Replace("*", "1");
                return Uri.IsWellFormedUriString(testPattern, UriKind.Absolute);
            }
            catch
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Validates if a URL is properly formatted.
    /// </summary>
    private static bool IsValidOriginUrl(string url)
    {
        return Uri.IsWellFormedUriString(url, UriKind.Absolute);
    }

    /// <summary>
    /// Checks if an origin matches a wildcard pattern.
    /// </summary>
    private static bool MatchesWildcardPattern(string pattern, string origin)
    {
        if (!pattern.Contains('*'))
            return false;

        try
        {
            // Parse both pattern and origin URLs
            var patternWithoutWildcard = pattern.Replace("*", "WILDCARD");
            var patternUri = new Uri(patternWithoutWildcard);
            var originUri = new Uri(origin);

            // Check scheme
            if (patternUri.Scheme != originUri.Scheme)
                return false;

            // Check port if specified in pattern
            if (!pattern.Contains(":*"))
            {
                if (patternUri.Port != originUri.Port)
                    return false;
            }

            // Check host with wildcard matching
            var patternHost = patternUri.Host.Replace("WILDCARD", "*");
            return MatchesHostPattern(patternHost, originUri.Host);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Matches a host against a pattern with wildcards.
    /// </summary>
    private static bool MatchesHostPattern(string pattern, string host)
    {
        if (pattern == "*")
            return true;

        if (!pattern.Contains('*'))
            return string.Equals(pattern, host, StringComparison.OrdinalIgnoreCase);

        // Handle subnet patterns like "192.168.1.*"
        if (pattern.EndsWith(".*"))
        {
            var prefix = pattern[..^2]; // Remove ".*"
            return host.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        // Handle other wildcard patterns
        var regexPattern = "^" + pattern.Replace("*", ".*") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(host, regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}