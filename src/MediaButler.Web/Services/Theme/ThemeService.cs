using Microsoft.JSInterop;

namespace MediaButler.Web.Services.Theme;

/// <summary>
/// Simple theme service following "Simple Made Easy" principles.
/// Manages light/dark theme switching without complecting with other concerns.
/// </summary>
public interface IThemeService
{
    Task<string> GetCurrentThemeAsync();
    Task SetThemeAsync(string theme);
    Task<string> GetSystemThemeAsync();
    Task ToggleThemeAsync();
    event Action<string> ThemeChanged;
}

public class ThemeService : IThemeService
{
    private readonly IJSRuntime _jsRuntime;
    private const string StorageKey = "mediabutler-theme";
    private const string DefaultTheme = "light";
    
    public event Action<string>? ThemeChanged;

    public ThemeService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<string> GetCurrentThemeAsync()
    {
        try
        {
            var storedTheme = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            return storedTheme ?? await GetSystemThemeAsync();
        }
        catch
        {
            return DefaultTheme;
        }
    }

    public async Task SetThemeAsync(string theme)
    {
        try
        {
            // Validate theme
            if (theme != "light" && theme != "dark" && theme != "system")
            {
                theme = DefaultTheme;
            }

            // Store preference
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, theme);
            
            // Apply theme to document
            var effectiveTheme = theme == "system" ? await GetSystemThemeAsync() : theme;
            await ApplyThemeToDocumentAsync(effectiveTheme);
            
            // Notify subscribers
            ThemeChanged?.Invoke(theme);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting theme: {ex.Message}");
        }
    }

    public async Task<string> GetSystemThemeAsync()
    {
        try
        {
            var prefersDark = await _jsRuntime.InvokeAsync<bool>("window.matchMedia", "(prefers-color-scheme: dark)");
            return prefersDark ? "dark" : "light";
        }
        catch
        {
            return "light";
        }
    }

    public async Task ToggleThemeAsync()
    {
        var currentTheme = await GetCurrentThemeAsync();
        var newTheme = currentTheme == "light" ? "dark" : "light";
        await SetThemeAsync(newTheme);
    }

    private async Task ApplyThemeToDocumentAsync(string theme)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("document.documentElement.setAttribute", "data-theme", theme);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error applying theme to document: {ex.Message}");
        }
    }
}