using System.Diagnostics;
using System.Text;

namespace MediaButler.API.Middleware;

/// <summary>
/// Request/response logging middleware with correlation ID generation and structured logging.
/// Implements Sprint 1.4.3 requirements following "Simple Made Easy" principles.
/// </summary>
public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;
    private readonly IConfiguration _configuration;

    private const string CorrelationIdHeaderName = "X-Correlation-ID";
    private const string RequestIdHeaderName = "X-Request-ID";

    public RequestResponseLoggingMiddleware(
        RequestDelegate next, 
        ILogger<RequestResponseLoggingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Generate or extract correlation ID
        var correlationId = GenerateOrExtractCorrelationId(context);
        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[CorrelationIdHeaderName] = correlationId;

        // Skip logging for filtered paths
        if (ShouldSkipLogging(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var requestBody = await LogRequestAsync(context.Request, correlationId);

        // Capture response
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            await LogResponseAsync(context, correlationId, stopwatch.ElapsedMilliseconds, responseBody);
            
            // Copy response back to original stream
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }

    private static string GenerateOrExtractCorrelationId(HttpContext context)
    {
        // Try to get from headers first
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var correlationIds))
        {
            var correlationId = correlationIds.FirstOrDefault();
            if (!string.IsNullOrEmpty(correlationId))
                return correlationId;
        }

        if (context.Request.Headers.TryGetValue(RequestIdHeaderName, out var requestIds))
        {
            var requestId = requestIds.FirstOrDefault();
            if (!string.IsNullOrEmpty(requestId))
                return requestId;
        }

        // Generate new unique correlation ID
        return Guid.NewGuid().ToString("D")[..8]; // Short 8-character ID for ARM32 efficiency
    }

    private async Task<string?> LogRequestAsync(HttpRequest request, string correlationId)
    {
        string? requestBody = null;
        
        // Selective body logging based on content type
        if (ShouldLogRequestBody(request))
        {
            requestBody = await ReadRequestBodyAsync(request);
        }

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestMethod"] = request.Method,
            ["RequestPath"] = request.Path.Value ?? "",
            ["QueryString"] = request.QueryString.Value ?? "",
            ["UserAgent"] = GetHeaderValue(request, "User-Agent"),
            ["RemoteIP"] = GetClientIpAddress(request),
            ["ContentType"] = request.ContentType ?? "",
            ["ContentLength"] = request.ContentLength ?? 0
        });

        _logger.LogInformation(
            "HTTP Request {Method} {Path} started with Content-Type: {ContentType}",
            request.Method,
            request.Path.Value,
            request.ContentType ?? "none");

        if (!string.IsNullOrEmpty(requestBody))
        {
            _logger.LogDebug(
                "Request Body for {Method} {Path}: {RequestBody}",
                request.Method,
                request.Path.Value,
                requestBody);
        }

        return requestBody;
    }

    private async Task LogResponseAsync(
        HttpContext context, 
        string correlationId, 
        long durationMs,
        MemoryStream responseBody)
    {
        var request = context.Request;
        var response = context.Response;

        string? responseBodyText = null;
        if (ShouldLogResponseBody(response))
        {
            responseBody.Seek(0, SeekOrigin.Begin);
            responseBodyText = await new StreamReader(responseBody).ReadToEndAsync();
            responseBody.Seek(0, SeekOrigin.Begin);
        }

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["StatusCode"] = response.StatusCode,
            ["ResponseSize"] = responseBody.Length,
            ["DurationMs"] = durationMs,
            ["ContentType"] = response.ContentType ?? "",
            ["PerformanceHeaders"] = GetPerformanceHeaders(response)
        });

        var logLevel = GetLogLevelForStatusCode(response.StatusCode);
        
        _logger.Log(logLevel,
            "HTTP Response {Method} {Path} completed with {StatusCode} in {Duration}ms - Size: {ResponseSize}",
            request.Method,
            request.Path.Value,
            response.StatusCode,
            durationMs,
            responseBody.Length);

        if (!string.IsNullOrEmpty(responseBodyText))
        {
            _logger.LogDebug(
                "Response Body for {Method} {Path}: {ResponseBody}",
                request.Method,
                request.Path.Value,
                responseBodyText);
        }

        // Performance threshold monitoring
        var thresholdMs = _configuration.GetValue<int>("Serilog:ARM32Optimization:PerformanceThresholdMs", 1000);
        if (durationMs > thresholdMs)
        {
            _logger.LogWarning(
                "Slow request detected: {Method} {Path} took {Duration}ms (threshold: {Threshold}ms)",
                request.Method,
                request.Path.Value,
                durationMs,
                thresholdMs);
        }
    }

    private static bool ShouldSkipLogging(PathString path)
    {
        return path.StartsWithSegments("/api/health") ||
               path.StartsWithSegments("/swagger") ||
               path.StartsWithSegments("/_framework") ||
               path.StartsWithSegments("/favicon.ico") ||
               path.StartsWithSegments("/robots.txt");
    }

    private static bool ShouldLogRequestBody(HttpRequest request)
    {
        if (request.ContentLength is null or 0)
            return false;

        var contentType = request.ContentType?.ToLowerInvariant() ?? "";
        
        return contentType.Contains("application/json") ||
               contentType.Contains("application/xml") ||
               contentType.Contains("text/") ||
               contentType.Contains("application/x-www-form-urlencoded");
    }

    private static bool ShouldLogResponseBody(HttpResponse response)
    {
        var contentType = response.ContentType?.ToLowerInvariant() ?? "";
        
        return (response.StatusCode >= 400) && // Only log error response bodies
               (contentType.Contains("application/json") ||
                contentType.Contains("application/xml") ||
                contentType.Contains("text/"));
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        
        using var reader = new StreamReader(
            request.Body,
            encoding: Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        
        return body;
    }

    private static string GetClientIpAddress(HttpRequest request)
    {
        // Check common proxy headers first
        var forwardedFor = GetHeaderValue(request, "X-Forwarded-For");
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = GetHeaderValue(request, "X-Real-IP");
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static string GetHeaderValue(HttpRequest request, string headerName)
    {
        return request.Headers.TryGetValue(headerName, out var values) 
            ? values.ToString() 
            : "";
    }

    private static Dictionary<string, string> GetPerformanceHeaders(HttpResponse response)
    {
        var headers = new Dictionary<string, string>();
        
        var performanceHeaders = new[] { "X-Response-Time", "X-Memory-Delta", "X-Memory-Total" };
        
        foreach (var headerName in performanceHeaders)
        {
            if (response.Headers.ContainsKey(headerName))
            {
                headers[headerName] = response.Headers[headerName].ToString();
            }
        }

        return headers;
    }

    private static LogLevel GetLogLevelForStatusCode(int statusCode)
    {
        return statusCode switch
        {
            >= 500 => LogLevel.Error,
            >= 400 => LogLevel.Warning,
            _ => LogLevel.Information
        };
    }
}

/// <summary>
/// Extension methods for registering the request/response logging middleware.
/// </summary>
public static class RequestResponseLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestResponseLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestResponseLoggingMiddleware>();
    }
}