using MediaButler.API.Configuration;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, configuration) => 
{
    var assemblyVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0";
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.WithProperty("Version", assemblyVersion);
});

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
    Version = "1.0.0",
    Status = "Ready",
    Documentation = "/swagger",
    HealthCheck = "/api/health",
    Timestamp = DateTime.UtcNow
});

app.Run();

// Make Program class accessible for testing
public partial class Program { }
