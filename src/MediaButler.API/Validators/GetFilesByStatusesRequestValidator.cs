using FluentValidation;
using MediaButler.Core.Enums;
using MediaButler.Core.Models.Requests;

namespace MediaButler.API.Validators;

/// <summary>
/// Validator for GetFilesByStatusesRequest following "Simple Made Easy" principles.
/// Separates validation rules from controller logic for better testability and maintainability.
/// </summary>
public class GetFilesByStatusesRequestValidator : AbstractValidator<GetFilesByStatusesRequest>
{
    public GetFilesByStatusesRequestValidator()
    {
        // Pagination validation
        RuleFor(x => x.Skip)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Skip must be greater than or equal to 0.");

        RuleFor(x => x.Take)
            .InclusiveBetween(1, 100)
            .WithMessage("Take must be between 1 and 100.");

        // Status array validation
        RuleFor(x => x.Statuses)
            .NotEmpty()
            .WithMessage("At least one status must be provided.");

        // Individual status validation
        RuleForEach(x => x.Statuses)
            .NotEmpty()
            .WithMessage("Status values cannot be empty.")
            .Must(BeValidFileStatus)
            .WithMessage(status => $"Invalid status value: {status}. Valid values are: {string.Join(", ", Enum.GetNames<FileStatus>())}");

        // OrderBy validation (optional, but if provided must be valid)
        When(x => !string.IsNullOrWhiteSpace(x.OrderBy), () =>
        {
            RuleFor(x => x.OrderBy)
                .Must(BeValidOrderByColumn)
                .WithMessage("Invalid orderBy value. Valid values are: FileName, Category, Status, CreatedDate, LastUpdateDate");
        });
    }

    /// <summary>
    /// Validates that the status string can be parsed to a FileStatus enum.
    /// </summary>
    private static bool BeValidFileStatus(string status)
    {
        return !string.IsNullOrWhiteSpace(status) &&
               Enum.TryParse<FileStatus>(status, true, out _);
    }

    /// <summary>
    /// Validates that the orderBy column name is one of the allowed values.
    /// </summary>
    private static bool BeValidOrderByColumn(string? orderBy)
    {
        if (string.IsNullOrWhiteSpace(orderBy))
            return true;

        var validColumns = new[]
        {
            "FileName",
            "Category",
            "Status",
            "CreatedDate",
            "LastUpdateDate"
        };

        return validColumns.Contains(orderBy, StringComparer.OrdinalIgnoreCase);
    }
}
