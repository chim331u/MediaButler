namespace MediaButler.Shared.UI.Models;

/// <summary>
/// Represents a navigation menu item for shared UI components.
/// </summary>
public class NavigationItem
{
    /// <summary>
    /// Display text for the navigation item
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// Route/URL to navigate to when clicked
    /// </summary>
    public required string Route { get; set; }

    /// <summary>
    /// Icon class or name (e.g., Radzen icon name)
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Indicates if this item is currently active/selected
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Display order in navigation menu
    /// </summary>
    public int Order { get; set; }
}
