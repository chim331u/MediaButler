using MediaButler.Mobile.Models;
using MediaButler.Mobile.Services;
using System.Text.RegularExpressions;

namespace MediaButler.Mobile.Pages;

public partial class FirstRunSetupPage : ContentPage
{
    private readonly IApiConfigurationService _configService;
    private readonly IApiConnectionService _connectionService;
    private readonly IServiceProvider _serviceProvider;
    private ConnectionResult? _lastTestResult;

    public FirstRunSetupPage(
        IApiConfigurationService configService,
        IApiConnectionService connectionService,
        IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // Pre-fill with common values
        ProtocolPicker.SelectedIndex = 0; // http
        PortEntry.Text = "5271";
    }

    private void OnInputChanged(object? sender, TextChangedEventArgs e)
    {
        // Reset test result when user changes inputs
        _lastTestResult = null;
        SaveButton.IsEnabled = false;
        StatusContainer.IsVisible = false;
    }

    private async void OnTestConnectionClicked(object? sender, EventArgs e)
    {
        // Validate inputs
        var validationError = ValidateInputs();
        if (validationError != null)
        {
            ShowError(validationError);
            return;
        }

        // Create temporary endpoint for testing
        var endpoint = CreateEndpointFromInputs();

        // Show loading state
        SetLoadingState(true);
        StatusContainer.IsVisible = false;

        try
        {
            // Test connection
            _lastTestResult = await _connectionService.TestEndpointAsync(endpoint);

            if (_lastTestResult.IsSuccess)
            {
                ShowSuccess($"Connected successfully! Latency: {_lastTestResult.LatencyMs}ms");
                SaveButton.IsEnabled = true;
            }
            else
            {
                ShowError(_lastTestResult.ErrorMessage ?? "Connection failed");
                SaveButton.IsEnabled = false;
            }
        }
        catch (Exception ex)
        {
            ShowError($"Unexpected error: {ex.Message}");
            SaveButton.IsEnabled = false;
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_lastTestResult == null || !_lastTestResult.IsSuccess)
        {
            await DisplayAlert("Error", "Please test the connection first", "OK");
            return;
        }

        SetLoadingState(true);

        try
        {
            // Create configuration with tested endpoint
            var endpoint = CreateEndpointFromInputs();
            var configuration = new ApiConfiguration
            {
                ApiEndpoints = new List<ApiEndpoint> { endpoint },
                ConnectionSettings = new ConnectionSettings()
            };

            // Save configuration
            await _configService.SaveConfigurationAsync(configuration);

            // Navigate to main page
            await NavigateToMainPage();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save configuration: {ex.Message}", "OK");
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private async void OnSkipClicked(object? sender, EventArgs e)
    {
        var result = await DisplayAlert(
            "Use Default Configuration?",
            "This will use the default server configuration (http://10.0.2.2:5271 for Android emulator). You can change this later in settings.",
            "Continue",
            "Cancel");

        if (!result)
            return;

        SetLoadingState(true);

        try
        {
            // Create default configuration
            var defaultConfig = ApiConfiguration.CreateDefault();
            await _configService.SaveConfigurationAsync(defaultConfig);

            // Navigate to main page
            await NavigateToMainPage();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save default configuration: {ex.Message}", "OK");
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private ApiEndpoint CreateEndpointFromInputs()
    {
        var protocol = ProtocolPicker.SelectedItem?.ToString() ?? "http";
        var host = HostEntry.Text?.Trim() ?? string.Empty;
        var port = int.Parse(PortEntry.Text?.Trim() ?? "5271");

        return new ApiEndpoint
        {
            Name = "Primary",
            Protocol = protocol,
            Host = host,
            Port = port,
            Priority = 1,
            Enabled = true
        };
    }

    private string? ValidateInputs()
    {
        // Validate protocol
        if (ProtocolPicker.SelectedIndex < 0)
        {
            return "Please select a protocol (HTTP or HTTPS)";
        }

        // Validate host
        var host = HostEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            return "Please enter a server address";
        }

        // Basic validation for IP or hostname
        if (!IsValidHostname(host) && !IsValidIpAddress(host))
        {
            return "Please enter a valid IP address or hostname";
        }

        // Validate port
        var portText = PortEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(portText))
        {
            return "Please enter a port number";
        }

        if (!int.TryParse(portText, out var port) || port < 1 || port > 65535)
        {
            return "Please enter a valid port number (1-65535)";
        }

        return null; // All validations passed
    }

    private static bool IsValidIpAddress(string host)
    {
        // Simple IPv4 validation
        var ipPattern = @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";
        return Regex.IsMatch(host, ipPattern);
    }

    private static bool IsValidHostname(string host)
    {
        // Basic hostname validation (letters, numbers, dots, hyphens)
        var hostnamePattern = @"^[a-zA-Z0-9]([a-zA-Z0-9\-\.]*[a-zA-Z0-9])?$";
        return Regex.IsMatch(host, hostnamePattern) && host.Length <= 253;
    }

    private void ShowSuccess(string message)
    {
        StatusContainer.IsVisible = true;
        StatusIcon.Text = "✅";
        StatusIcon.TextColor = Colors.Green;
        StatusLabel.Text = message;
        StatusLabel.TextColor = Colors.Green;
    }

    private void ShowError(string message)
    {
        StatusContainer.IsVisible = true;
        StatusIcon.Text = "❌";
        StatusIcon.TextColor = Colors.Red;
        StatusLabel.Text = message;
        StatusLabel.TextColor = Colors.Red;
    }

    private void SetLoadingState(bool isLoading)
    {
        LoadingIndicator.IsRunning = isLoading;
        LoadingIndicator.IsVisible = isLoading;
        TestConnectionButton.IsEnabled = !isLoading;
        SaveButton.IsEnabled = !isLoading && _lastTestResult?.IsSuccess == true;
        ProtocolPicker.IsEnabled = !isLoading;
        HostEntry.IsEnabled = !isLoading;
        PortEntry.IsEnabled = !isLoading;
    }

    private Task NavigateToMainPage()
    {
        // Use modern Windows API to update the page
        if (Application.Current?.Windows.Count > 0)
        {
            var mainPage = _serviceProvider.GetRequiredService<MainPage>();
            Application.Current.Windows[0].Page = new NavigationPage(mainPage);
        }

        return Task.CompletedTask;
    }
}
