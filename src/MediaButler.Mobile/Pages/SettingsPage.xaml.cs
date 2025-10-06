using MediaButler.Mobile.Models;
using MediaButler.Mobile.Services;

namespace MediaButler.Mobile.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly IApiConfigurationService _configService;
    private readonly IApiConnectionService _connectionService;
    private readonly IServiceProvider _serviceProvider;

    public SettingsPage(
        IApiConfigurationService configService,
        IApiConnectionService connectionService,
        IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // Subscribe to configuration changes
        _configService.ConfigurationChanged += OnConfigurationChanged;

        // Load initial data
        LoadSettingsAsync();
    }

    private async void LoadSettingsAsync()
    {
        try
        {
            var config = _configService.GetCurrentConfiguration();

            // Load endpoints
            LoadEndpoints(config);

            // Load advanced settings
            LoadAdvancedSettings(config.ConnectionSettings);

            // Test current connection
            await UpdateConnectionStatusAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load settings: {ex.Message}", "OK");
        }
    }

    private void LoadEndpoints(ApiConfiguration config)
    {
        EndpointsContainer.Children.Clear();

        var endpoints = config.ApiEndpoints.OrderBy(e => e.Priority).ToList();

        if (!endpoints.Any())
        {
            EmptyStateLabel.IsVisible = true;
            return;
        }

        EmptyStateLabel.IsVisible = false;

        for (int i = 0; i < endpoints.Count; i++)
        {
            var endpoint = endpoints[i];
            var endpointView = CreateEndpointView(endpoint, i > 0);
            EndpointsContainer.Children.Add(endpointView);
        }
    }

    private View CreateEndpointView(ApiEndpoint endpoint, bool addDivider)
    {
        var container = new VerticalStackLayout { Spacing = 0 };

        // Add divider if not first item
        if (addDivider)
        {
            container.Children.Add(new BoxView
            {
                HeightRequest = 1,
                Color = Colors.LightGray,
                Margin = new Thickness(10, 0)
            });
        }

        // Main endpoint card
        var mainLayout = new VerticalStackLayout
        {
            Padding = new Thickness(15, 10),
            Spacing = 8
        };

        // Header row: Name + Status + Priority
        var headerRow = new HorizontalStackLayout { Spacing = 10 };

        headerRow.Children.Add(new Label
        {
            Text = endpoint.Name,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Center
        });

        var statusDot = new Label
        {
            Text = endpoint.Enabled ? "●" : "○",
            FontSize = 16,
            TextColor = endpoint.Enabled ? Colors.Green : Colors.Gray,
            VerticalOptions = LayoutOptions.Center
        };
        headerRow.Children.Add(statusDot);

        // Spacer
        headerRow.Children.Add(new Label { WidthRequest = 0, HorizontalOptions = LayoutOptions.FillAndExpand });

        var priorityLabel = new Label
        {
            Text = $"Priority: {endpoint.Priority}",
            FontSize = 12,
            TextColor = Colors.Gray,
            VerticalOptions = LayoutOptions.Center
        };
        headerRow.Children.Add(priorityLabel);

        mainLayout.Children.Add(headerRow);

        // URL row
        var urlLabel = new Label
        {
            Text = endpoint.GetBaseUrl(),
            FontSize = 14,
            TextColor = Colors.Gray
        };
        mainLayout.Children.Add(urlLabel);

        // Action buttons row
        var actionsRow = new HorizontalStackLayout { Spacing = 10, Margin = new Thickness(0, 5, 0, 0) };

        var testButton = new Button
        {
            Text = "Test",
            FontSize = 12,
            Padding = new Thickness(10, 5),
            BackgroundColor = Colors.LightBlue,
            TextColor = Colors.White,
            CornerRadius = 5
        };
        testButton.Clicked += async (s, e) => await OnTestSingleEndpointClicked(endpoint);
        actionsRow.Children.Add(testButton);

        var editButton = new Button
        {
            Text = "Edit",
            FontSize = 12,
            Padding = new Thickness(10, 5),
            BackgroundColor = Colors.Orange,
            TextColor = Colors.White,
            CornerRadius = 5
        };
        editButton.Clicked += async (s, e) => await OnEditEndpointClicked(endpoint);
        actionsRow.Children.Add(editButton);

        var deleteButton = new Button
        {
            Text = "Delete",
            FontSize = 12,
            Padding = new Thickness(10, 5),
            BackgroundColor = Colors.Red,
            TextColor = Colors.White,
            CornerRadius = 5
        };
        deleteButton.Clicked += async (s, e) => await OnDeleteEndpointClicked(endpoint);
        actionsRow.Children.Add(deleteButton);

        mainLayout.Children.Add(actionsRow);
        container.Children.Add(mainLayout);

        return container;
    }

    private void LoadAdvancedSettings(ConnectionSettings settings)
    {
        HealthCheckPathLabel.Text = settings.HealthCheckPath;
        TimeoutLabel.Text = $"{settings.TimeoutSeconds} seconds";
        RetryAttemptsLabel.Text = settings.RetryAttempts.ToString();
    }

    private async Task UpdateConnectionStatusAsync()
    {
        SetLoadingState(true);

        try
        {
            var result = await _connectionService.DiscoverActiveEndpointAsync();

            if (result.IsSuccess)
            {
                StatusDot.TextColor = Colors.Green;
                StatusLabel.Text = "Connected";

                ActiveEndpointContainer.IsVisible = true;
                ActiveEndpointLabel.Text = $"{result.Endpoint.Name} ({result.Endpoint.GetBaseUrl()})";

                if (result.LatencyMs.HasValue)
                {
                    LatencyContainer.IsVisible = true;
                    LatencyLabel.Text = $"{result.LatencyMs}ms";
                }
            }
            else
            {
                StatusDot.TextColor = Colors.Red;
                StatusLabel.Text = $"Disconnected: {result.ErrorMessage}";
                ActiveEndpointContainer.IsVisible = false;
                LatencyContainer.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            StatusDot.TextColor = Colors.Red;
            StatusLabel.Text = $"Error: {ex.Message}";
            ActiveEndpointContainer.IsVisible = false;
            LatencyContainer.IsVisible = false;
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private async void OnTestConnectionClicked(object? sender, EventArgs e)
    {
        await UpdateConnectionStatusAsync();
    }

    private async void OnReloadConfigClicked(object? sender, EventArgs e)
    {
        SetLoadingState(true);

        try
        {
            await _configService.ReloadConfigurationAsync();
            await DisplayAlert("Success", "Configuration reloaded successfully", "OK");
            LoadSettingsAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to reload configuration: {ex.Message}", "OK");
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private async void OnAddEndpointClicked(object? sender, EventArgs e)
    {
        var modal = _serviceProvider.GetRequiredService<AddEditEndpointModal>();
        await Navigation.PushModalAsync(new NavigationPage(modal));
    }

    private async Task OnEditEndpointClicked(ApiEndpoint endpoint)
    {
        var modal = _serviceProvider.GetRequiredService<AddEditEndpointModal>();
        modal.SetEndpoint(endpoint);
        await Navigation.PushModalAsync(new NavigationPage(modal));
    }

    private async Task OnDeleteEndpointClicked(ApiEndpoint endpoint)
    {
        var result = await DisplayAlert(
            "Delete Endpoint",
            $"Are you sure you want to delete '{endpoint.Name}'?",
            "Delete",
            "Cancel");

        if (!result)
            return;

        SetLoadingState(true);

        try
        {
            await _configService.RemoveEndpointAsync(endpoint.Name);
            LoadSettingsAsync();
            await DisplayAlert("Success", $"Endpoint '{endpoint.Name}' deleted", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to delete endpoint: {ex.Message}", "OK");
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private async Task OnTestSingleEndpointClicked(ApiEndpoint endpoint)
    {
        SetLoadingState(true);

        try
        {
            var result = await _connectionService.TestEndpointAsync(endpoint);

            if (result.IsSuccess)
            {
                await DisplayAlert(
                    "Success",
                    $"Connection successful!\nLatency: {result.LatencyMs}ms",
                    "OK");
            }
            else
            {
                await DisplayAlert(
                    "Failed",
                    $"Connection failed:\n{result.ErrorMessage}",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Test failed: {ex.Message}", "OK");
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private async void OnTestAllEndpointsClicked(object? sender, EventArgs e)
    {
        SetLoadingState(true);

        try
        {
            var results = await _connectionService.TestAllEndpointsAsync();

            var resultMessages = results.Select(r =>
                $"• {r.Endpoint.Name}: {(r.IsSuccess ? $"✅ {r.LatencyMs}ms" : $"❌ {r.ErrorMessage}")}");

            var message = string.Join("\n", resultMessages);

            await DisplayAlert("Test Results", message, "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Test failed: {ex.Message}", "OK");
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private void OnAdvancedToggled(object? sender, EventArgs e)
    {
        AdvancedContainer.IsVisible = !AdvancedContainer.IsVisible;
    }

    private void OnConfigurationChanged(object? sender, ApiConfiguration config)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadSettingsAsync();
        });
    }

    private void SetLoadingState(bool isLoading)
    {
        LoadingIndicator.IsRunning = isLoading;
        LoadingIndicator.IsVisible = isLoading;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _configService.ConfigurationChanged -= OnConfigurationChanged;
    }
}
