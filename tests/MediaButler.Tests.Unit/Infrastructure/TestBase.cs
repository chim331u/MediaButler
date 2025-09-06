using FluentAssertions;
using FluentAssertions.Extensions;

namespace MediaButler.Tests.Unit.Infrastructure;

/// <summary>
/// Base class for unit tests providing common test infrastructure.
/// Follows "Simple Made Easy" principles with minimal setup and clear test organization.
/// </summary>
public abstract class TestBase
{
    /// <summary>
    /// Protected constructor ensures this class is used as a base class.
    /// </summary>
    protected TestBase()
    {
        // Configure FluentAssertions for consistent formatting
        AssertionOptions.AssertEquivalencyUsing(options =>
        {
            options.Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, 1.Seconds()))
                   .WhenTypeIs<DateTime>();
                   
            options.Using<DateTime?>(ctx => 
            {
                if (ctx.Subject.HasValue && ctx.Expectation.HasValue)
                {
                    ctx.Subject.Should().BeCloseTo(ctx.Expectation.Value, 1.Seconds());
                }
                else
                {
                    ctx.Subject.Should().Be(ctx.Expectation);
                }
            }).WhenTypeIs<DateTime?>();
            
            return options;
        });
    }

    /// <summary>
    /// Creates a test DateTime that's deterministic and timezone-safe.
    /// Use this instead of DateTime.Now for consistent test results.
    /// </summary>
    protected static DateTime BaseTestDateTime => new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Creates a test DateTime offset by the specified minutes.
    /// Useful for testing time-based operations and sequences.
    /// </summary>
    protected static DateTime TestDateTime(int offsetMinutes = 0) => BaseTestDateTime.AddMinutes(offsetMinutes);

    /// <summary>
    /// Generates a test SHA256 hash with a specific suffix for easy identification.
    /// </summary>
    protected static string TestHash(string suffix = "001") => 
        $"abcdef1234567890123456789012345678901234567890123456789012345{suffix.PadLeft(3, '0')}";

    /// <summary>
    /// Creates a valid test file path with the given filename.
    /// </summary>
    protected static string TestFilePath(string fileName = "test.mkv") => 
        $"/test/downloads/{fileName}";

    /// <summary>
    /// Creates a valid test library path with the given series and filename.
    /// </summary>
    protected static string TestLibraryPath(string series, string fileName) => 
        $"/test/library/{series}/{fileName}";
}