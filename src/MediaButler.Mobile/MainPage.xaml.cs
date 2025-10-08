using MediaButler.Mobile.Pages;

namespace MediaButler.Mobile;

public partial class MainPage : ContentPage
{
	private readonly IServiceProvider _serviceProvider;

	public MainPage(IServiceProvider serviceProvider)
	{
		InitializeComponent();
		_serviceProvider = serviceProvider;
	}

	private async void OnSettingsClicked(object? sender, EventArgs e)
	{
		var settingsPage = _serviceProvider.GetRequiredService<SettingsPage>();
		await Navigation.PushAsync(settingsPage);
	}
}
