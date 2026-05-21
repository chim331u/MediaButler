using System.Reflection;
using MediaButler.API.Configuration;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseMediaButlerLogging();

// Modularized Service Registration
builder.Services.AddMediaButlerCors(builder.Configuration)
                .AddMediaButlerPersistence(builder.Configuration)
                .AddMediaButlerServices(builder.Configuration)
                .AddMediaButlerHangfire(builder.Configuration)
                .AddMediaButlerMLServices(builder.Configuration)
                .AddMediaButlerExternalClients(builder.Configuration)
                .AddMediaButlerApiConfig();

var app = builder.Build();

// Modularized Middleware Pipeline
await app.UseMediaButlerDatabase();

app.UseMediaButlerDocumentation(app.Environment);
app.UseMediaButlerMiddleware();

// Final Endpoints
app.MapControllers();

app.MapGet("/", () => new
{
    Service = "MediaButler API",
    Version = Program.Version,
    Status = "Ready",
    Documentation = "/swagger",
    HealthCheck = "/api/health",
    Timestamp = DateTime.UtcNow
});

app.Run();

// Make Program class accessible for testing and expose dynamic version
public partial class Program 
{
    public static readonly string Version = 
        System.Reflection.Assembly.GetEntryAssembly()?.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
        ?? "1.0.0";
}
