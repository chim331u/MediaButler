using MediaButler.Mobile.Pages;
using MediaButler.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace MediaButler.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		// Register HTTP client factory
		builder.Services.AddHttpClient();

		// Register MediaButler API services
		builder.Services.AddSingleton<IApiConfigurationService, ApiConfigurationService>();
		builder.Services.AddSingleton<IApiConnectionService, ApiConnectionService>();
		builder.Services.AddSingleton<IApiClient, ApiClient>();

		// Register pages
		builder.Services.AddTransient<FirstRunSetupPage>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddTransient<AddEditEndpointModal>();

		return builder.Build();
	}
}
