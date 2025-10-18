using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediaButler.API.Services;

/// <summary>
/// Hosted service that registers recurring jobs on application startup.
/// Recurring jobs are scheduled tasks that run on a cron schedule (e.g., daily, weekly).
/// </summary>
public class RecurringJobRegistrationService : IHostedService
{
    private readonly ILogger<RecurringJobRegistrationService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IRecurringJobManager _recurringJobManager;

    public RecurringJobRegistrationService(
        ILogger<RecurringJobRegistrationService> logger,
        IConfiguration configuration,
        IRecurringJobManager recurringJobManager)
    {
        _logger = logger;
        _configuration = configuration;
        _recurringJobManager = recurringJobManager;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Registering Hangfire recurring jobs...");

        var recurringJobsConfig = _configuration.GetSection("Hangfire:RecurringJobs");

        // File Discovery Job - Scan watch folders for new files (replaces FileSystemWatcher)
        var fileDiscoveryConfig = recurringJobsConfig.GetSection("FileDiscovery");
        if (fileDiscoveryConfig.GetValue<bool>("Enabled", true))
        {
            var cronExpression = fileDiscoveryConfig["CronExpression"] ?? "*/5 * * * *"; // Every 5 minutes
            _logger.LogInformation("Registering FileDiscovery job with cron: {Cron}", cronExpression);

            _recurringJobManager.AddOrUpdate<MediaButler.API.Jobs.Recurring.FileDiscoveryJob>(
                "file-discovery",
                job => job.ScanForNewFilesAsync(),
                cronExpression,
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Local
                });

            _logger.LogInformation("FileDiscovery recurring job registered successfully");
        }

        // ML Model Training Job - Retrain classification model
        var modelTrainingConfig = recurringJobsConfig.GetSection("ModelTraining");
        if (modelTrainingConfig.GetValue<bool>("Enabled", false))
        {
            var cronExpression = modelTrainingConfig["CronExpression"] ?? "0 3 * * 0";
            _logger.LogInformation("Registering ModelTraining job with cron: {Cron}", cronExpression);

            _recurringJobManager.AddOrUpdate<MediaButler.API.Jobs.Recurring.ModelTrainingJob>(
                "model-training",
                job => job.TrainModelAsync(),
                cronExpression,
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Local
                });

            _logger.LogInformation("ModelTraining recurring job registered successfully");
        }

        // Database Maintenance Job - VACUUM and ANALYZE
        var dbMaintenanceConfig = recurringJobsConfig.GetSection("DatabaseMaintenance");
        if (dbMaintenanceConfig.GetValue<bool>("Enabled", false))
        {
            var cronExpression = dbMaintenanceConfig["CronExpression"] ?? "0 2 * * 0";
            _logger.LogInformation("Registering DatabaseMaintenance job with cron: {Cron}", cronExpression);

            // TODO: Implement in Sprint 4
            // _recurringJobManager.AddOrUpdate<DatabaseMaintenanceJob>(
            //     "database-maintenance",
            //     job => job.OptimizeDatabasesAsync(JobCancellationToken.Null),
            //     cronExpression,
            //     new RecurringJobOptions { Queue = "low-priority", TimeZone = TimeZoneInfo.Local });
        }

        // Log Cleanup Job - Remove old log files
        var logCleanupConfig = recurringJobsConfig.GetSection("LogCleanup");
        if (logCleanupConfig.GetValue<bool>("Enabled", false))
        {
            var cronExpression = logCleanupConfig["CronExpression"] ?? "0 2 * * *";
            _logger.LogInformation("Registering LogCleanup job with cron: {Cron}", cronExpression);

            _recurringJobManager.AddOrUpdate<MediaButler.API.Jobs.Recurring.LogCleanupJob>(
                "log-cleanup",
                job => job.CleanupOldLogsAsync(),
                cronExpression,
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Local
                });

            _logger.LogInformation("LogCleanup recurring job registered successfully");
        }

        _logger.LogInformation("Recurring jobs registration completed");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Recurring job registration service stopping");
        return Task.CompletedTask;
    }
}
