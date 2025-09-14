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
        httpClient.BaseAddress = new Uri("http://localhost:5000/");
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

await builder.Build().RunAsync();