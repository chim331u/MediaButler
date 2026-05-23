using MediaButler.Core.Models.Responses;

namespace MediaButler.API.Middleware;

/// <summary>
/// Middleware to secure the API using a simple API Key configuration.
/// Excludes specified paths (like Swagger, Health checks) from authentication.
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private const string APIKEYNAME = "X-Api-Key";

    public ApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        // Allow OPTIONS preflight requests to bypass authentication
        if (string.Equals(context.Request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value;
        var excludedPaths = configuration.GetSection("Security:ExcludedPaths").Get<string[]>() ?? Array.Empty<string>();
        
        // Allow excluded paths to bypass authentication
        if (excludedPaths.Any(p => path != null && path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Require API Key for all other paths
        string? extractedApiKey = null;
        if (context.Request.Headers.TryGetValue(APIKEYNAME, out var headerValue))
        {
            extractedApiKey = headerValue;
        }
        else if (context.Request.Query.TryGetValue("apiKey", out var queryValue))
        {
            extractedApiKey = queryValue;
        }
        else if (context.Request.Query.TryGetValue("api-key", out var queryValue2))
        {
            extractedApiKey = queryValue2;
        }

        if (string.IsNullOrEmpty(extractedApiKey))
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(ApiResponse.CreateError("UNAUTHORIZED", "API Key is missing"));
            return;
        }

        var configuredApiKey = configuration.GetValue<string>("Security:ApiKey");
        
        if (string.IsNullOrEmpty(configuredApiKey) || !configuredApiKey.Equals(extractedApiKey))
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(ApiResponse.CreateError("UNAUTHORIZED", "Invalid API Key"));
            return;
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for registering the API Key middleware.
/// </summary>
public static class ApiKeyMiddlewareExtensions
{
    /// <summary>
    /// Adds the API Key authentication middleware to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyMiddleware>();
    }
}
