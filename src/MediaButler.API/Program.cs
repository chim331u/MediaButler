using Microsoft.EntityFrameworkCore;
using MediaButler.Data;
using MediaButler.Data.Repositories;
using MediaButler.Data.UnitOfWork;
using MediaButler.Services;
using MediaButler.Services.Interfaces;
using MediaButler.Services.Configuration;
using MediaButler.Core.Services;
using MediaButler.Core.Configuration;
using MediaButler.API.Middleware;
using MediaButler.API.Filters;
using MediaButler.API.Hubs;
using MediaButler.API.Services;
using MediaButler.ML.Extensions;
using MediaButler.Services.Background;
using MediaButler.Services.FileOperations;
using MediaButler.Services.Extensions;
using Serilog;
using Serilog.Events;
using FluentValidation;
using FluentValidation.AspNetCore;
using MediaButler.API.Configuration;
using Hangfire;
using Hangfire.Storage.SQLite;
using Hangfire.Dashboard;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog from appsettings.json following "Simple Made Easy" principles
builder.Host.UseSerilog((context, configuration) => 
    configuration.ReadFrom.Configuration(context.Configuration));

// Configure CORS settings from appsettings.json
var corsSettingsSection = builder.Configuration.GetSection(CorsSettings.SectionName);
builder.Services.Configure<CorsSettings>(corsSettingsSection);

// Get CORS settings and validate configuration
var corsSettings = new CorsSettings();
corsSettingsSection.Bind(corsSettings);

// Validate CORS configuration
var corsValidationErrors = corsSettings.Validate().ToList();
if (corsValidationErrors.Any())
{
    var errorMessages = string.Join("; ", corsValidationErrors.Select(e => e.ErrorMessage));
    throw new InvalidOperationException($"Invalid CORS configuration: {errorMessages}");
}

// Add CORS with configuration-based policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("ConfigurablePolicy", policyBuilder =>
    {
        // Configure origin validation with wildcard support
        policyBuilder.SetIsOriginAllowed(origin => corsSettings.IsOriginAllowed(origin));

        // Configure methods
        if (corsSettings.AllowedMethods.Any())
            policyBuilder.WithMethods(corsSettings.AllowedMethods.ToArray());
        else
            policyBuilder.AllowAnyMethod();

        // Configure headers
        if (corsSettings.AllowedHeaders.Any())
            policyBuilder.WithHeaders(corsSettings.AllowedHeaders.ToArray());
        else
            policyBuilder.AllowAnyHeader();

        // Configure credentials
        if (corsSettings.AllowCredentials)
            policyBuilder.AllowCredentials();
    });
});

// Add services to the container
builder.Services.AddDbContext<MediaButlerDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly("MediaButler.Data")));

// Add repository and unit of work
builder.Services.AddScoped<ITrackedFileRepository, TrackedFileRepository>();
builder.Services.AddScoped<IFileOrganizationStateRepository, FileOrganizationStateRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Add configuration service (single source of truth for configuration)
builder.Services.AddSingleton<IMediaButlerConfiguration, MediaButlerConfiguration>();

// Add application services
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<IRollbackService, RollbackService>();
builder.Services.AddScoped<IErrorClassificationService, ErrorClassificationService>();
builder.Services.AddScoped<IOrganizationStateService, OrganizationStateService>();
builder.Services.AddScoped<IOrganizationValidator, MediaButler.Services.Validation.OrganizationValidator>();
builder.Services.AddScoped<IFileOrganizationService, FileOrganizationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

// Add ML training services
builder.Services.AddScoped<MediaButler.Services.ML.IDatabaseTrainingService, MediaButler.Services.ML.DatabaseTrainingService>();

// Add batch file processing services
builder.Services.AddScoped<IFileActionsService, FileActionsService>();

// Add Hangfire services with SQLite storage (client mode - job enqueueing only)
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

// Add Hangfire server (combined mode - both enqueue and execute jobs)
var hangfireConfig = builder.Configuration.GetSection("Hangfire:Server");
builder.Services.AddHangfireServer(options =>
{
    options.ServerName = hangfireConfig["ServerName"] ?? "mediabutler-api-worker";
    options.WorkerCount = hangfireConfig.GetValue<int>("WorkerCount", 2);
    options.Queues = hangfireConfig.GetSection("Queues").Get<string[]>() ?? new[] { "critical", "default", "low-priority" };
    options.HeartbeatInterval = TimeSpan.Parse(hangfireConfig["HeartbeatInterval"] ?? "00:00:30");
    options.ServerCheckInterval = TimeSpan.Parse(hangfireConfig["ServerCheckInterval"] ?? "00:05:00");
    options.SchedulePollingInterval = TimeSpan.FromSeconds(15);
});

// Register Hangfire job classes
builder.Services.AddScoped<MediaButler.API.Jobs.Batch.BatchFileProcessingJob>();
builder.Services.AddScoped<IBatchFileProcessor, MediaButler.API.Jobs.Batch.BatchFileProcessingJob>();
builder.Services.AddScoped<MediaButler.API.Jobs.Recurring.FileDiscoveryJob>();

// Register batch job services (progress reporting and throttling)
builder.Services.AddScoped<IProgressReporter, SignalRProgressReporter>();
builder.Services.AddScoped<IBatchThrottler, Arm32BatchThrottler>();

// Register SignalR notification client for job-to-hub communication
builder.Services.AddHttpClient<MediaButler.API.Services.SignalRNotificationClient>(client =>
{
    var signalRConfig = builder.Configuration.GetSection("SignalRClient");
    var apiBaseUrl = signalRConfig["ApiBaseUrl"] ?? "http://localhost:5000";
    var timeoutSeconds = signalRConfig.GetValue<int>("TimeoutSeconds", 30);

    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    client.DefaultRequestHeaders.Add("User-Agent", "MediaButler-API/1.0");
});
builder.Services.AddSingleton<MediaButler.API.Services.SignalRNotificationClient>();

// Register recurring job registration service
builder.Services.AddHostedService<MediaButler.API.Services.RecurringJobRegistrationService>();

// Add SignalR services
builder.Services.AddSignalR();
builder.Services.AddSingleton<ISignalRNotificationService, SignalRNotificationService>();

// Add SignalR integration for file discovery notifications
builder.Services.AddHostedService<FileDiscoverySignalRService>();

// Add file operation services
builder.Services.AddScoped<IFileOperationService, FileOperationService>();
builder.Services.AddScoped<IPathGenerationService, PathGenerationService>();

// Add ML services with configuration
builder.Services.AddMediaButlerML(builder.Configuration);

// Add background processing services with configuration
builder.Services.AddBackgroundServices(builder.Configuration);

// Add custom background task queue (temporary - will be removed in Sprint 3)
// This is needed by FileActionsService until batch processing is migrated to Hangfire
builder.Services.AddCustomBackgroundTaskQueue(100);

// Add FluentValidation
builder.Services.AddFluentValidationAutoValidation()
    .AddFluentValidationClientsideAdapters();

// Register validators
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Add API services with validation
builder.Services.AddControllers(options =>
{
    // Add global model validation filter
    options.Filters.Add<ModelValidationFilter>();

    // Configure JSON options for consistent formatting
    options.RespectBrowserAcceptHeader = true;
    options.ReturnHttpNotAcceptable = true;
});

builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = false;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "MediaButler API", 
        Version = "v1",
        Description = "Intelligent media file organization system with ML-powered classification"
    });
    
    // Include XML comments for API documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// Add JSON serialization configuration
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = false;
});

var app = builder.Build();

// Ensure database is created and migrations are applied
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MediaButlerDbContext>();
    try
    {
        await context.Database.MigrateAsync();
        Log.Information("Database migration completed successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Failed to migrate database");
        throw;
    }
}

// Configure the HTTP request pipeline with middleware order
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MediaButler API v1");
        c.RoutePrefix = "swagger";
        c.DisplayRequestDuration();
        c.EnableValidator();
    });

    // Add Hangfire Dashboard for job monitoring (dev only - no auth)
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        DashboardTitle = "MediaButler Background Jobs",
        StatsPollingInterval = 10000 // 10 seconds
    });
}

// Add request/response logging first for complete request tracking
app.UseRequestResponseLogging();

// Add performance monitoring with ARM32 optimization
app.UsePerformanceMonitoring();

// Add global exception handling with structured logging
app.UseGlobalExceptionHandler();

app.UseHttpsRedirection();
app.UseRouting();

// Use configurable CORS policy for all environments
app.UseCors("ConfigurablePolicy");

// Map controllers
app.MapControllers();

// Map SignalR hubs
app.MapHub<NotificationHub>("/notifications");
app.MapHub<FileProcessingHub>("/file-processing");

// Enhanced root endpoint with API information
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
