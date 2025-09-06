namespace MediaButler.Tests.Integration.Infrastructure;

/// <summary>
/// Defines pre-configured test data scenarios for integration testing.
/// Each scenario represents a specific testing context with appropriate data.
/// Follows "Simple Made Easy" principles by providing clear, purpose-built scenarios.
/// </summary>
public enum TestDataScenario
{
    /// <summary>
    /// Complete file processing workflow with files in all statuses.
    /// Includes configurations and processing logs.
    /// Use for: End-to-end workflow testing, status transition validation.
    /// </summary>
    Workflow,

    /// <summary>
    /// Large volume of files for performance and load testing.
    /// Creates hundreds/thousands of files with realistic data distribution.
    /// Use for: Performance testing, pagination testing, bulk operations.
    /// </summary>
    Performance,

    /// <summary>
    /// Active and soft-deleted files for query filter testing.
    /// Tests BaseEntity soft delete functionality and global query filters.
    /// Use for: Soft delete behavior, data isolation testing.
    /// </summary>
    SoftDelete,

    /// <summary>
    /// Files with known classification patterns for ML accuracy testing.
    /// Includes various confidence levels and series name patterns.
    /// Use for: ML classification testing, confidence threshold validation.
    /// </summary>
    Classification,

    /// <summary>
    /// Files with various error conditions and retry scenarios.
    /// Includes error logs and different error types.
    /// Use for: Error handling testing, retry logic validation.
    /// </summary>
    Error,

    /// <summary>
    /// Minimal data set for simple integration tests.
    /// Single file and configuration for basic functionality testing.
    /// Use for: Simple integration tests, quick validation scenarios.
    /// </summary>
    Minimal
}