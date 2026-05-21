using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MediaButler.Web;
using MediaButler.Web.Interfaces;
using MediaButler.Web.Services;
using MediaButler.Web.Models;
using MediaButler.Shared.UI.Models;
using MediaButler.Shared.UI.Services;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure environment-specific settings for Blazor WebAssembly
// Explicit environment detection and configuration loading
var environment = builder.HostEnvironment.Environment;

// Force development configuration for local development
// In Blazor WASM, environment detection can be unreliable
var isDevelopment = builder.HostEnvironment.IsDevelopment() ||
                   builder.HostEnvironment.BaseAddress.Contains("localhost") ||
                   builder.HostEnvironment.BaseAddress.Contains("127.0.0.1");

// Register and validate ApiSettings configuration
var apiSettingsSection = builder.Configuration.GetSection("ApiSettings");
builder.Services.Configure<ApiSettings>(apiSettingsSection);

// Get ApiSettings and validate configuration
var apiSettings = new ApiSettings();
apiSettingsSection.Bind(apiSettings);

if (!apiSettings.IsValid)
{
    throw new InvalidOperationException(
        $"Invalid API configuration. Please ensure 'ApiSettings:BaseUrl' is properly configured in appsettings.json. " +
        $"Current value: '{apiSettings.BaseUrl}'");
}

Console.WriteLine($"Environment: {environment}, IsDevelopment: {isDevelopment}, API URL: {apiSettings.BaseUrl}");

// Simple HttpClient registration following "Simple Made Easy" principles
// One named client per service boundary - no complex configurations braided together
builder.Services.AddHttpClient<IHttpClientService, HttpClientService>(client =>
{
    client.BaseAddress = new Uri(apiSettings.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    if (!string.IsNullOrEmpty(apiSettings.ApiKey))
    {
        client.DefaultRequestHeaders.Add("X-Api-Key", apiSettings.ApiKey);
    }
});

// MediaButler API services - following "Simple Made Easy" principles
builder.Services.AddScoped<IHealthApiService, HealthApiService>();
builder.Services.AddScoped<IFilesApiService, FilesApiService>();
builder.Services.AddScoped<ITrainingApiService, TrainingApiService>();

// SignalR notification service - centralized real-time communication
// SSE notification service - Centralized Server-Sent Events
builder.Services.AddScoped<ISseNotificationService, SseNotificationService>();

// Radzen services
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<TooltipService>();
builder.Services.AddScoped<ContextMenuService>();

await builder.Build().RunAsync();
