using MediaButler.Batch.Services;
using Hangfire;
using Hangfire.Storage.SQLite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

// Configure Serilog from appsettings.json
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

try
{
    Log.Information("Starting MediaButler.Batch - Hangfire Background Worker");

    var builder = Host.CreateApplicationBuilder(args);

    // Configure Serilog
    builder.Services.AddSerilog();

    // Add Hangfire services with SQLite storage (server mode - job execution)
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSQLiteStorage(
            builder.Configuration.GetConnectionString("HangfireConnection"),
            new SQLiteStorageOptions
            {
                QueuePollInterval = TimeSpan.FromSeconds(15),
                JobExpirationCheckInterval = TimeSpan.FromHours(1),
                InvisibilityTimeout = TimeSpan.FromMinutes(30)
            }
        ));

    // Add Hangfire server (server mode - WorkerCount = 2 for ARM32)
    var hangfireConfig = builder.Configuration.GetSection("Hangfire:Server");
    builder.Services.AddHangfireServer(options =>
    {
        options.ServerName = hangfireConfig["ServerName"] ?? "mediabutler-batch-worker";
        options.WorkerCount = hangfireConfig.GetValue<int>("WorkerCount", 2);
        options.Queues = hangfireConfig.GetSection("Queues").Get<string[]>() ?? new[] { "critical", "default", "low-priority" };
        options.HeartbeatInterval = TimeSpan.Parse(hangfireConfig["HeartbeatInterval"] ?? "00:00:30");
        options.ServerCheckInterval = TimeSpan.Parse(hangfireConfig["ServerCheckInterval"] ?? "00:05:00");
        options.SchedulePollingInterval = TimeSpan.FromSeconds(15);
    });

    // Register application services (will be added in future steps)
    // TODO: Add services from MediaButler.Services (IFileOrganizationService, etc.)

    // Add hosted service for recurring job registration
    builder.Services.AddHostedService<RecurringJobRegistrationService>();

    var host = builder.Build();

    Log.Information("Hangfire server configured with {WorkerCount} workers",
        hangfireConfig.GetValue<int>("WorkerCount", 2));

    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "MediaButler.Batch terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
