using MediaButler.API.Models.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MediaButler.API.Filters;

/// <summary>
/// Action filter that handles model validation and returns consistent error responses
/// for invalid models following "Simple Made Easy" principles.
/// </summary>
public class ModelValidationFilter : ActionFilterAttribute
{
    /// <summary>
    /// Called before the action method executes to validate model state.
    /// </summary>
    /// <param name="context">The action executing context</param>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var validationErrors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
                );

            var errorResponse = ApiResponse.CreateError(
                "VALIDATION_ERROR",
                "One or more validation errors occurred",
                new { ValidationErrors = validationErrors }
            );

            context.Result = new BadRequestObjectResult(errorResponse);
        }

        base.OnActionExecuting(context);
    }
}

/// <summary>
/// Model validation attribute that can be applied to controllers or actions
/// to enable automatic model validation with consistent error responses.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ValidateModelAttribute : ActionFilterAttribute
{
    /// <summary>
    /// Called before the action method executes to validate model state.
    /// </summary>
    /// <param name="context">The action executing context</param>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var validationErrors = ExtractValidationErrors(context.ModelState);
            
            var errorResponse = CreateValidationErrorResponse(validationErrors);
            
            context.Result = new BadRequestObjectResult(errorResponse);
        }

        base.OnActionExecuting(context);
    }

    /// <summary>
    /// Extracts validation errors from model state into a structured format.
    /// </summary>
    /// <param name="modelState">The model state to extract errors from</param>
    /// <returns>Dictionary of field names to error messages</returns>
    private static Dictionary<string, string[]> ExtractValidationErrors(
        Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState)
    {
        return modelState
            .Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => FormatFieldName(kvp.Key),
                kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
            );
    }

    /// <summary>
    /// Formats field names to be more user-friendly (converts PascalCase to readable format).
    /// </summary>
    /// <param name="fieldName">The field name to format</param>
    /// <returns>Formatted field name</returns>
    private static string FormatFieldName(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName))
            return fieldName;

        // Convert PascalCase to readable format
        // e.g., "FilePath" -> "File Path"
        return string.Concat(
            fieldName.Select((x, i) => i > 0 && char.IsUpper(x) ? " " + x : x.ToString())
        );
    }

    /// <summary>
    /// Creates a consistent validation error response.
    /// </summary>
    /// <param name="validationErrors">The validation errors</param>
    /// <returns>Standardized API response for validation errors</returns>
    private static ApiResponse CreateValidationErrorResponse(Dictionary<string, string[]> validationErrors)
    {
        var totalErrors = validationErrors.Values.Sum(errors => errors.Length);
        var errorMessage = totalErrors == 1 
            ? "A validation error occurred" 
            : $"{totalErrors} validation errors occurred";

        return ApiResponse.CreateError(
            "VALIDATION_ERROR", 
            errorMessage, 
            new { ValidationErrors = validationErrors }
        );
    }
}