namespace MediaButler.Shared.UI.Models;

/// <summary>
/// Shared route constants for consistent navigation across Web and Mobile platforms.
/// Following "Simple Made Easy" principles - single source of truth for routes.
/// </summary>
public static class Routes
{
    /// <summary>
    /// Home/Dashboard page - file overview and statistics
    /// </summary>
    public const string Home = "/";

    /// <summary>
    /// Files listing page - view and manage tracked files
    /// </summary>
    public const string Files = "/files";

    /// <summary>
    /// Settings page - API endpoint configuration
    /// </summary>
    public const string Settings = "/settings";

    /// <summary>
    /// First-run setup wizard - initial API endpoint configuration
    /// </summary>
    public const string FirstRunSetup = "/setup";

    /// <summary>
    /// Health check page - system status and diagnostics
    /// </summary>
    public const string Health = "/health";

    /// <summary>
    /// Training page - ML model training and management
    /// </summary>
    public const string Training = "/training";
}
