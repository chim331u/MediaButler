namespace MediaButler.Core.Enums;

/// <summary>
/// Specifies the data type of a configuration setting value.
/// This enum helps with type validation and proper serialization/deserialization
/// of configuration values stored as JSON strings.
/// </summary>
/// <remarks>
/// This enum follows "Simple Made Easy" principles by providing explicit, 
/// non-overlapping categories for configuration data types without complecting
/// storage concerns with business logic.
/// </remarks>
public enum ConfigurationDataType
{
    /// <summary>
    /// String/text value that can contain any textual data.
    /// </summary>
    String = 0,

    /// <summary>
    /// Numeric integer value (32-bit signed integer).
    /// </summary>
    Integer = 1,

    /// <summary>
    /// Boolean true/false value.
    /// </summary>
    Boolean = 2,

    /// <summary>
    /// File system path value that should be validated for existence and permissions.
    /// </summary>
    Path = 3,

    /// <summary>
    /// Complex JSON object or array that requires JSON parsing.
    /// </summary>
    Json = 4
}