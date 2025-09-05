using MediaButler.API.Models.Response;
using System.Net;
using System.Text.Json;
using System.Diagnostics;

namespace MediaButler.API.Middleware;

/// <summary>
/// Global exception handling middleware with structured logging and environment-aware error details.
/// Implements Sprint 1.4.3 requirements following "Simple Made Easy" principles.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the GlobalExceptionMiddleware.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline</param>
    /// <param name="logger">Logger for exception logging</param>
    /// <param name="environment">Web host environment for environment-aware error handling</param>
    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Invokes the middleware to handle the HTTP request.
    /// </summary>
    /// <param name="context">The HTTP context</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var correlationId = context.Items["CorrelationId"]?.ToString() ?? "unknown";
            
            // Structured error logging with full context
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["RequestMethod"] = context.Request.Method,
                ["RequestPath"] = context.Request.Path.Value ?? "",
                ["QueryString"] = context.Request.QueryString.Value ?? "",
                ["UserAgent"] = context.Request.Headers.UserAgent.ToString(),
                ["RemoteIP"] = context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                ["ExceptionType"] = ex.GetType().Name,
                ["StackTrace"] = _environment.IsDevelopment() ? ex.StackTrace : null
            });

            _logger.LogError(ex, 
                "Unhandled exception {ExceptionType} occurred while processing {Method} {Path}: {Message}",
                ex.GetType().Name,
                context.Request.Method,
                context.Request.Path.Value,
                ex.Message);
            
            await HandleExceptionAsync(context, ex, correlationId);
        }
    }

    /// <summary>
    /// Handles exceptions by converting them to standardized API error responses.
    /// </summary>
    /// <param name="context">The HTTP context</param>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="correlationId">The correlation ID for tracking</param>
    private async Task HandleExceptionAsync(HttpContext context, Exception exception, string correlationId)
    {
        context.Response.ContentType = "application/json";

        var response = exception switch
        {
            ArgumentNullException nullEx => CreateErrorResponse(
                HttpStatusCode.BadRequest, 
                "MISSING_REQUIRED_PARAMETER", 
                nullEx.Message),
            
            ArgumentException argEx => CreateErrorResponse(
                HttpStatusCode.BadRequest, 
                "INVALID_ARGUMENT", 
                argEx.Message),
            
            UnauthorizedAccessException => CreateErrorResponse(
                HttpStatusCode.Unauthorized, 
                "UNAUTHORIZED", 
                "Access denied"),
            
            FileNotFoundException fileEx => CreateErrorResponse(
                HttpStatusCode.NotFound, 
                "FILE_NOT_FOUND", 
                fileEx.Message),
            
            DirectoryNotFoundException dirEx => CreateErrorResponse(
                HttpStatusCode.NotFound, 
                "DIRECTORY_NOT_FOUND", 
                dirEx.Message),
            
            TaskCanceledException => CreateErrorResponse(
                HttpStatusCode.RequestTimeout, 
                "REQUEST_TIMEOUT", 
                "The request timed out"),
            
            OperationCanceledException => CreateErrorResponse(
                HttpStatusCode.RequestTimeout, 
                "OPERATION_CANCELLED", 
                "The operation was cancelled"),
            
            InvalidOperationException invalidEx => CreateErrorResponse(
                HttpStatusCode.Conflict, 
                "INVALID_OPERATION", 
                invalidEx.Message),
            
            NotSupportedException notSupportedEx => CreateErrorResponse(
                HttpStatusCode.NotImplemented, 
                "NOT_SUPPORTED", 
                notSupportedEx.Message),
            
            _ => CreateErrorResponse(
                HttpStatusCode.InternalServerError, 
                "INTERNAL_SERVER_ERROR", 
                "An unexpected error occurred",
                GetEnvironmentAwareDetails(exception, correlationId))
        };

        context.Response.StatusCode = (int)response.StatusCode;

        var jsonResponse = JsonSerializer.Serialize(response.ApiResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        await context.Response.WriteAsync(jsonResponse);
    }

    /// <summary>
    /// Creates a standardized error response.
    /// </summary>
    /// <param name="statusCode">HTTP status code</param>
    /// <param name="errorCode">Application-specific error code</param>
    /// <param name="message">Error message</param>
    /// <param name="details">Optional error details</param>
    /// <returns>Error response with HTTP status code</returns>
    private static (HttpStatusCode StatusCode, ApiResponse ApiResponse) CreateErrorResponse(
        HttpStatusCode statusCode, 
        string errorCode, 
        string message, 
        object? details = null)
    {
        var apiResponse = ApiResponse.CreateError(errorCode, message, details);
        return (statusCode, apiResponse);
    }

    /// <summary>
    /// Creates environment-aware error details for security-conscious error handling.
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="correlationId">The correlation ID for tracking</param>
    /// <returns>Error details appropriate for the current environment</returns>
    private object GetEnvironmentAwareDetails(Exception exception, string correlationId)
    {
        var details = new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["Timestamp"] = DateTime.UtcNow,
            ["ExceptionType"] = exception.GetType().Name
        };

        // Include detailed information only in development environment
        if (_environment.IsDevelopment())
        {
            details["Message"] = exception.Message;
            details["StackTrace"] = exception.StackTrace ?? "";
            
            if (exception.InnerException != null)
            {
                details["InnerException"] = new
                {
                    Type = exception.InnerException.GetType().Name,
                    Message = exception.InnerException.Message
                };
            }
        }
        else
        {
            // Production: Security-conscious error handling
            details["Message"] = "An error occurred while processing your request";
            details["ContactSupport"] = $"Please contact support with Correlation ID: {correlationId}";
        }

        return details;
    }
}

/// <summary>
/// Extension methods for registering the global exception middleware.
/// </summary>
public static class GlobalExceptionMiddlewareExtensions
{
    /// <summary>
    /// Adds the global exception handling middleware to the application pipeline.
    /// </summary>
    /// <param name="builder">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionMiddleware>();
    }
}