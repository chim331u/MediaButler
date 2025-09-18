using MediaButler.Web;
using MediaButler.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Register root components
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HTTP client for API communication
builder.Services.AddScoped(sp => 
{
    var httpClient = new HttpClient();
    
    // Configure API base address based on environment
    var baseAddress = builder.HostEnvironment.BaseAddress;
    if (baseAddress.Contains("localhost") || baseAddress.Contains("127.0.0.1"))
    {
        // Development: API runs on port 5000
        httpClient.BaseAddress = new Uri("http://localhost:5271/");
    }
    else
    {
        // Production: API and Web are served together
        httpClient.BaseAddress = new Uri(baseAddress);
    }
    
    // Configure JSON options for API compatibility
    httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    
    return httpClient;
});

// Register UI services
builder.Services.AddScoped<IApiClient, ApiClient>();
builder.Services.AddScoped<IFileManagementService, FileManagementService>();

// Register system services
builder.Services.AddScoped<MediaButler.Web.Services.System.ISystemStatusService, MediaButler.Web.Services.System.SystemStatusService>();

// Register component architecture services
builder.Services.AddScoped<MediaButler.Web.Services.State.IStateService, MediaButler.Web.Services.State.StateService>();
builder.Services.AddScoped<MediaButler.Web.Services.Events.IEventBus, MediaButler.Web.Services.Events.EventBus>();
builder.Services.AddScoped<MediaButler.Web.Services.Lifecycle.IComponentLifecycleService, MediaButler.Web.Services.Lifecycle.ComponentLifecycleService>();

// Register design system services
builder.Services.AddScoped<MediaButler.Web.Services.Icons.IIconService, MediaButler.Web.Services.Icons.IconService>();
builder.Services.AddScoped<MediaButler.Web.Services.Theme.IThemeService, MediaButler.Web.Services.Theme.ThemeService>();

// Register real-time communication services
builder.Services.AddScoped<MediaButler.Web.Services.RealTime.ISignalRService, MediaButler.Web.Services.RealTime.SignalRService>();
builder.Services.AddScoped<MediaButler.Web.Services.RealTime.IConnectionManager, MediaButler.Web.Services.RealTime.ConnectionManager>();
builder.Services.AddScoped<MediaButler.Web.Services.RealTime.IOfflineService, MediaButler.Web.Services.RealTime.OfflineService>();

// Register notification services
builder.Services.AddScoped<MediaButler.Web.Services.Notifications.INotificationService, MediaButler.Web.Services.Notifications.NotificationService>();

await builder.Build().RunAsync();