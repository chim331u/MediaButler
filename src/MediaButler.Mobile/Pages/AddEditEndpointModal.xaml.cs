using MediaButler.Mobile.Models;
using MediaButler.Mobile.Services;
using System.Text.RegularExpressions;

namespace MediaButler.Mobile.Pages;

public partial class AddEditEndpointModal : ContentPage
{
    private readonly IApiConfigurationService _configService;
    private readonly IApiConnectionService _connectionService;
    private ApiEndpoint? _existingEndpoint;
    private bool _isEditMode;
    private ConnectionResult? _lastTestResult;

    public AddEditEndpointModal(
        IApiConfigurationService configService,
        IApiConnectionService connectionService)
    {
        InitializeComponent();
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));

        // Default values for new endpoint
        ProtocolPicker.SelectedIndex = 0; // http
        PortEntry.Text = "5271";
        PriorityEntry.Text = "1";
        EnabledSwitch.IsToggled = true;
    }

    /// <summary>
    /// Sets an existing endpoint for editing.
    /// </summary>
    public void SetEndpoint(ApiEndpoint endpoint)
    {
        _existingEndpoint = endpoint;
        _isEditMode = true;
        Title = $"Edit Endpoint: {endpoint.Name}";

        // Populate form with existing values
        NameEntry.Text = endpoint.Name;
        ProtocolPicker.SelectedItem = endpoint.Protocol;
        HostEntry.Text = endpoint.Host;
        PortEntry.Text = endpoint.Port.ToString();
        PriorityEntry.Text = endpoint.Priority.ToString();
        EnabledSwitch.IsToggled = endpoint.Enabled;
    }

    private async void OnTestConnectionClicked(object? sender, EventArgs e)
    {
        // Validate inputs first
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
            }
            else
            {
                ShowError(_lastTestResult.ErrorMessage ?? "Connection failed");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Unexpected error: {ex.Message}");
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        // Validate inputs
        var validationError = ValidateInputs();
        if (validationError != null)
        {
            await DisplayAlert("Validation Error", validationError, "OK");
            return;
        }

        SetLoadingState(true);

        try
        {
            var endpoint = CreateEndpointFromInputs();

            if (_isEditMode && _existingEndpoint != null)
            {
                // Update existing endpoint
                await _configService.UpdateEndpointAsync(_existingEndpoint.Name, endpoint);
                await DisplayAlert("Success", "Endpoint updated successfully", "OK");
            }
            else
            {
                // Add new endpoint
                await _configService.AddEndpointAsync(endpoint);
                await DisplayAlert("Success", "Endpoint added successfully", "OK");
            }

            // Close modal
            await Navigation.PopModalAsync();
        }
        catch (InvalidOperationException ex)
        {
            // Duplicate name or other business logic error
            await DisplayAlert("Error", ex.Message, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save endpoint: {ex.Message}", "OK");
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    private ApiEndpoint CreateEndpointFromInputs()
    {
        var name = NameEntry.Text?.Trim() ?? string.Empty;
        var protocol = ProtocolPicker.SelectedItem?.ToString() ?? "http";
        var host = HostEntry.Text?.Trim() ?? string.Empty;
        var port = int.Parse(PortEntry.Text?.Trim() ?? "5271");
        var priority = int.Parse(PriorityEntry.Text?.Trim() ?? "1");
        var enabled = EnabledSwitch.IsToggled;

        return new ApiEndpoint
        {
            Name = name,
            Protocol = protocol,
            Host = host,
            Port = port,
            Priority = priority,
            Enabled = enabled
        };
    }

    private string? ValidateInputs()
    {
        // Validate name
        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Please enter an endpoint name";
        }

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

        // Validate priority
        var priorityText = PriorityEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(priorityText))
        {
            return "Please enter a priority number";
        }

        if (!int.TryParse(priorityText, out var priority) || priority < 1)
        {
            return "Please enter a valid priority (1 or higher)";
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
        SaveButton.IsEnabled = !isLoading;
        NameEntry.IsEnabled = !isLoading;
        ProtocolPicker.IsEnabled = !isLoading;
        HostEntry.IsEnabled = !isLoading;
        PortEntry.IsEnabled = !isLoading;
        PriorityEntry.IsEnabled = !isLoading;
        EnabledSwitch.IsEnabled = !isLoading;
    }
}
