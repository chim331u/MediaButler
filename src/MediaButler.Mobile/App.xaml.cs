using MediaButler.Mobile.Pages;
using MediaButler.Mobile.Services;

namespace MediaButler.Mobile;

public partial class App : Application
{
	private readonly IApiConfigurationService _configService;
	private readonly IServiceProvider _serviceProvider;
	private Window? _mainWindow;

	public App(IApiConfigurationService configService, IServiceProvider serviceProvider)
	{
		InitializeComponent();
		_configService = configService;
		_serviceProvider = serviceProvider;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Create window with a loading page initially
		_mainWindow = new Window(new ContentPage { Title = "Loading..." }) { Title = "MediaButler" };

		// Initialize asynchronously after window is created
		InitializeAsync();

		return _mainWindow;
	}

	private async void InitializeAsync()
	{
		try
		{
			// Small delay to ensure window is fully created
			await Task.Delay(100);

			// Check if configuration exists (first-run detection)
			var hasConfig = await _configService.HasConfigurationAsync();

			Page initialPage;
			if (!hasConfig)
			{
				// First run - show setup page
				var setupPage = _serviceProvider.GetRequiredService<FirstRunSetupPage>();
				initialPage = new NavigationPage(setupPage);
			}
			else
			{
				// Load existing configuration
				await _configService.LoadConfigurationAsync();
				initialPage = new NavigationPage(new MainPage());
			}

			// Update window with initial page
			if (_mainWindow != null)
			{
				_mainWindow.Page = initialPage;
			}
		}
		catch (Exception ex)
		{
			// Log error and fallback to main page
			System.Diagnostics.Debug.WriteLine($"Initialization failed: {ex.Message}");
			System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");

			if (_mainWindow != null)
			{
				_mainWindow.Page = new NavigationPage(new MainPage());
			}
		}
	}
}
