using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace MediaButler.API.Configuration;

/// <summary>
/// Static configuration class for robust, dynamic Serilog logging with semantic versioning.
/// </summary>
public static class SerilogConfiguration
{
    /// <summary>
    /// Configures Serilog for the WebApplicationBuilder host, including dynamic version enrichment.
    /// </summary>
    public static ConfigureHostBuilder UseMediaButlerLogging(this ConfigureHostBuilder hostBuilder)
    {
        hostBuilder.UseSerilog((context, loggerConfiguration) =>
        {
            ConfigureLogging(loggerConfiguration, context.Configuration);
        });

        return hostBuilder;
    }

    /// <summary>
    /// Centralized logging configuration logic. Sets up enrichment, filters, and sinks.
    /// </summary>
    public static void ConfigureLogging(LoggerConfiguration loggerConfiguration, IConfiguration configuration)
    {
        // 1. Retrieve the Entry Assembly Version (SemVer dynamic extraction)
        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion 
                      ?? assembly?.GetName().Version?.ToString() 
                      ?? "1.0.0";

        // Remove git commit hashes or metadata added by tools like MinVer or SourceLink if present
        if (version.Contains('+'))
        {
            version = version.Split('+')[0];
        }

        // 2. Base Configuration & Enrichment (compatible with default Serilog.AspNetCore packages)
        loggerConfiguration
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Version", version) // Dynamic Version Enrichment!
            .ReadFrom.Configuration(configuration); // Allow appsettings.json overrides

        // 3. Define the output template containing structured JSON properties `{Properties:j}`
        const string outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}";

        // 4. Configure Sinks (Console and Rolling File)
        loggerConfiguration.WriteTo.Console(
            outputTemplate: outputTemplate,
            restrictedToMinimumLevel: LogEventLevel.Information
        );

        // Optional: Local file logging with retention and size limits
        var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "mediabutler-.log");
        loggerConfiguration.WriteTo.File(
            path: logPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            fileSizeLimitBytes: 10 * 1024 * 1024, // 10 MB
            rollOnFileSizeLimit: true,
            outputTemplate: outputTemplate,
            restrictedToMinimumLevel: LogEventLevel.Information
        );
    }
}
