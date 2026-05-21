using MediaButler.API.Configuration;
using MediaButler.API.Filters;
using MediaButler.API.Services;
using MediaButler.Core.Configuration;
using MediaButler.Core.Interfaces;
using MediaButler.Core.Services;
using MediaButler.Data;
using MediaButler.Data.Repositories;
using MediaButler.Data.Services;
using MediaButler.Data.UnitOfWork;
using MediaButler.ML.Extensions;
using MediaButler.Services;
using MediaButler.Services.Background;
using MediaButler.Services.Configuration;
using MediaButler.Services.Extensions;
using MediaButler.Services.FileOperations;
using MediaButler.Services.Interfaces;
using MediaButler.Services.Validation;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using Hangfire.Storage.SQLite;
using Serilog;
using FluentValidation;
using FluentValidation.AspNetCore;

namespace Microsoft.Extensions.DependencyInjection;

public static class MediaButlerServiceExtensions
{
    public static IServiceCollection AddMediaButlerCors(this IServiceCollection services, IConfiguration configuration)
    {
        var corsSettingsSection = configuration.GetSection(CorsSettings.SectionName);
        services.Configure<CorsSettings>(corsSettingsSection);

        var corsSettings = new CorsSettings();
        corsSettingsSection.Bind(corsSettings);

        var corsValidationErrors = corsSettings.Validate().ToList();
        if (corsValidationErrors.Any())
        {
            var errorMessages = string.Join("; ", corsValidationErrors.Select(e => e.ErrorMessage));
            throw new InvalidOperationException($"Invalid CORS configuration: {errorMessages}");
        }

        services.AddCors(options =>
        {
            options.AddPolicy("ConfigurablePolicy", policyBuilder =>
            {
                policyBuilder.SetIsOriginAllowed(origin => corsSettings.IsOriginAllowed(origin));

                if (corsSettings.AllowedMethods.Any())
                    policyBuilder.WithMethods(corsSettings.AllowedMethods.ToArray());
                else
                    policyBuilder.AllowAnyMethod();

                if (corsSettings.AllowedHeaders.Any())
                    policyBuilder.WithHeaders(corsSettings.AllowedHeaders.ToArray());
                else
                    policyBuilder.AllowAnyHeader();

                if (corsSettings.AllowCredentials)
                    policyBuilder.AllowCredentials();
            });
        });

        return services;
    }

    public static IServiceCollection AddMediaButlerPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<MediaButlerDbContext>(options =>
            options.UseSqlite(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly("MediaButler.Data")));

        services.AddScoped<ITrackedFileRepository, TrackedFileRepository>();
        services.AddScoped<IFileOrganizationStateRepository, FileOrganizationStateRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    public static IServiceCollection AddMediaButlerServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMediaButlerConfiguration, MediaButlerConfiguration>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IStatsService, StatsService>();
        services.AddScoped<IRollbackService, RollbackService>();
        services.AddScoped<IErrorClassificationService, ErrorClassificationService>();
        services.AddScoped<IOrganizationStateService, OrganizationStateService>();
        services.AddScoped<IOrganizationValidator, OrganizationValidator>();
        services.AddScoped<IFileOrganizationService, FileOrganizationService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IFileActionsService, FileActionsService>();
        services.AddScoped<IFileOperationService, FileOperationService>();
        services.AddScoped<IPathGenerationService, PathGenerationService>();
        services.AddScoped<IMlOrchestrationService, MlOrchestrationService>();

        // Background / Special services
        services.AddHostedService<MediaButler.API.Services.RecurringJobRegistrationService>();
        services.AddSingleton<MediaButler.API.Modules.RealTime.SSE.SseConnectionManager>();
        services.AddRealTimeModule();
        services.AddHostedService<FileDiscoverySignalRService>();

        // Batch job services
        services.AddScoped<IProgressReporter, SignalRProgressReporter>();
        services.AddScoped<IBatchThrottler, Arm32BatchThrottler>();
        
        services.AddBackgroundServices(configuration);
        services.AddCustomBackgroundTaskQueue(100);

        return services;
    }

    public static IServiceCollection AddMediaButlerHangfire(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHangfire(config => 
        {
            config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings();

            var useInMemory = configuration.GetValue<bool>("Hangfire:UseInMemoryStorage", true);

            if (useInMemory)
            {
                config.UseInMemoryStorage();
                Log.Information("Hangfire configured to use In-Memory storage");
            }
            else
            {
                config.UseSQLiteStorage(
                    configuration.GetConnectionString("HangfireConnection"),
                    new SQLiteStorageOptions
                    {
                        QueuePollInterval = TimeSpan.FromSeconds(15),
                        JobExpirationCheckInterval = TimeSpan.FromHours(1),
                        InvisibilityTimeout = TimeSpan.FromMinutes(30)
                    }
                );
                Log.Information("Hangfire configured to use SQLite storage");
            }
        });

        var hangfireConfig = configuration.GetSection("Hangfire:Server");
        services.AddHangfireServer(options =>
        {
            options.ServerName = hangfireConfig["ServerName"] ?? "mediabutler-api-worker";
            options.WorkerCount = hangfireConfig.GetValue<int>("WorkerCount", 2);
            options.Queues = hangfireConfig.GetSection("Queues").Get<string[]>() ?? new[] { "critical", "default", "low-priority" };
            options.HeartbeatInterval = TimeSpan.Parse(hangfireConfig["HeartbeatInterval"] ?? "00:00:30");
            options.ServerCheckInterval = TimeSpan.Parse(hangfireConfig["ServerCheckInterval"] ?? "00:05:00");
            options.SchedulePollingInterval = TimeSpan.FromSeconds(15);
        });

        // Jobs
        services.AddScoped<MediaButler.API.Jobs.Batch.BatchFileProcessingJob>();
        services.AddScoped<IBatchFileProcessor, MediaButler.API.Jobs.Batch.BatchFileProcessingJob>();
        services.AddScoped<MediaButler.API.Jobs.Recurring.FileDiscoveryJob>();
        services.AddScoped<MediaButler.API.Jobs.Recurring.LogCleanupJob>();
        services.AddScoped<MediaButler.API.Jobs.Recurring.ModelTrainingJob>();

        return services;
    }

    public static IServiceCollection AddMediaButlerMLServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMediaButlerML(configuration);
        services.AddScoped<MediaButler.Core.Interfaces.IMLPersistenceService, MediaButler.Data.Services.MLPersistenceService>();
        services.AddScoped<MediaButler.Services.ML.IDatabaseTrainingService, MediaButler.Services.ML.DatabaseTrainingService>();
        
        return services;
    }

    public static IServiceCollection AddMediaButlerExternalClients(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient("SignalRNotificationClient", (serviceProvider, client) =>
        {
            var signalRConfig = configuration.GetSection("SignalRClient");
            var apiBaseUrl = signalRConfig["ApiBaseUrl"] ?? "http://localhost:5271";
            var timeoutSeconds = signalRConfig.GetValue<int>("TimeoutSeconds", 30);

            client.BaseAddress = new Uri(apiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            client.DefaultRequestHeaders.Add("User-Agent", "MediaButler-API/1.0");
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        services.AddSingleton<SignalRNotificationClient>(serviceProvider =>
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("SignalRNotificationClient");
            var logger = serviceProvider.GetRequiredService<ILogger<SignalRNotificationClient>>();

            return new SignalRNotificationClient(httpClient, configuration, logger);
        });

        return services;
    }

    public static IServiceCollection AddMediaButlerApiConfig(this IServiceCollection services)
    {
        services.AddFluentValidationAutoValidation()
            .AddFluentValidationClientsideAdapters();

        services.AddValidatorsFromAssemblyContaining<MediaButler.API.Validators.BatchOrganizeRequestValidator>();

        services.AddControllers(options =>
        {
            options.Filters.Add<ModelValidationFilter>();
            options.RespectBrowserAcceptHeader = true;
            options.ReturnHttpNotAcceptable = true;
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        });

        services.Configure<RouteOptions>(options =>
        {
            options.LowercaseUrls = true;
            options.LowercaseQueryStrings = false;
        });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { 
                Title = "MediaButler API", 
                Version = "v1",
                Description = "Intelligent media file organization system with ML-powered classification"
            });
            
            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                c.IncludeXmlComments(xmlPath);
            }
        });

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.SerializerOptions.WriteIndented = false;
        });

        return services;
    }
}
