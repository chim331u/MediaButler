using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MediaButler.Web;
using MediaButler.Web.Interfaces;
using MediaButler.Web.Services;
using MediaButler.Web.Models;
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

// Register ApiSettings configuration
builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));

// Simple HttpClient registration following "Simple Made Easy" principles
// One named client per service boundary - no complex configurations braided together
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5000/";

Console.WriteLine($"Environment: {environment}, IsDevelopment: {isDevelopment}, API URL: {apiBaseUrl}");

builder.Services.AddHttpClient<IHttpClientService, HttpClientService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// MediaButler API services - following "Simple Made Easy" principles
builder.Services.AddScoped<IHealthApiService, HealthApiService>();
builder.Services.AddScoped<IFilesApiService, FilesApiService>();
builder.Services.AddScoped<ITrainingApiService, TrainingApiService>();

// SignalR notification service - centralized real-time communication
builder.Services.AddSingleton<ISignalRNotificationService, SignalRNotificationService>();

// Radzen services
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<TooltipService>();
builder.Services.AddScoped<ContextMenuService>();

await builder.Build().RunAsync();
