namespace MediaButler.Core.Enums;

/// <summary>
/// Specifies the severity level of a log entry.
/// This enum aligns with standard logging frameworks while maintaining
/// clear separation between different levels of diagnostic information.
/// </summary>
/// <remarks>
/// Levels are ordered by severity, with Trace being the most verbose
/// and Critical being the most severe. This follows standard logging
/// conventions and integrates well with .NET logging infrastructure.
/// </remarks>
public enum LogLevel
{
    /// <summary>
    /// Very detailed diagnostic information, typically only enabled during development.
    /// Used for fine-grained debugging and tracing execution flow.
    /// </summary>
    Trace = 0,

    /// <summary>
    /// Detailed diagnostic information useful for debugging.
    /// Contains information about internal application state and decision points.
    /// </summary>
    Debug = 1,

    /// <summary>
    /// General informational messages about application flow.
    /// Indicates normal operation and major milestones.
    /// </summary>
    Information = 2,

    /// <summary>
    /// Warning messages indicating potential issues that don't prevent operation.
    /// Situations that are unusual but recoverable.
    /// </summary>
    Warning = 3,

    /// <summary>
    /// Error messages indicating failures that affect specific operations.
    /// The application can continue running but some functionality may be impaired.
    /// </summary>
    Error = 4,

    /// <summary>
    /// Critical error messages indicating severe failures that may cause application termination.
    /// Represents the most severe level of problems.
    /// </summary>
    Critical = 5
}