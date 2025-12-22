namespace MediaButler.Shared.UI.Services;

/// <summary>
/// Platform-agnostic navigation service interface.
/// Implementations: BlazorNavigationService (Web), MauiNavigationService (Mobile)
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Navigates to the specified route.
    /// </summary>
    /// <param name="route">The route to navigate to (e.g., "/files", "/settings")</param>
    /// <param name="forceLoad">Force page reload even if already on the route</param>
    Task NavigateToAsync(string route, bool forceLoad = false);

    /// <summary>
    /// Navigates back to the previous page.
    /// </summary>
    Task NavigateBackAsync();

    /// <summary>
    /// Displays an alert dialog with OK button.
    /// </summary>
    /// <param name="title">Alert title</param>
    /// <param name="message">Alert message</param>
    /// <param name="okText">OK button text (default: "OK")</param>
    Task DisplayAlertAsync(string title, string message, string okText = "OK");

    /// <summary>
    /// Displays a confirmation dialog with OK/Cancel buttons.
    /// </summary>
    /// <param name="title">Confirmation title</param>
    /// <param name="message">Confirmation message</param>
    /// <param name="okText">OK button text (default: "OK")</param>
    /// <param name="cancelText">Cancel button text (default: "Cancel")</param>
    /// <returns>True if user clicked OK, false if cancelled</returns>
    Task<bool> DisplayConfirmAsync(string title, string message, string okText = "OK", string cancelText = "Cancel");
}
