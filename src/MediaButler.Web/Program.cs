using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MediaButler.Web;
using MediaButler.Web.Interfaces;
using MediaButler.Web.Services;
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

// Simple HttpClient registration following "Simple Made Easy" principles
// One named client per service boundary - no complex configurations braided together
var apiBaseUrl = isDevelopment
    ? "https://localhost:7103/"  // Development URL - updated to use HTTPS
    : builder.Configuration["ApiSettings:BaseUrl"] ?? "http://192.168.1.5:30109/"; // Production URL

Console.WriteLine($"Environment: {environment}, IsDevelopment: {isDevelopment}, API URL: {apiBaseUrl}");

builder.Services.AddHttpClient<IHttpClientService, HttpClientService>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// MediaButler API services - following "Simple Made Easy" principles
builder.Services.AddScoped<IHealthApiService, HealthApiService>();
builder.Services.AddScoped<IConfigApiService, ConfigApiService>();

// Radzen services
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<TooltipService>();
builder.Services.AddScoped<ContextMenuService>();

await builder.Build().RunAsync();
