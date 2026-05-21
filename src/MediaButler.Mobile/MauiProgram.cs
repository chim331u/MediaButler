using MediaButler.Mobile.Components.Interfaces;
using MediaButler.Mobile.Components.Service;
using MediaButler.Shared.UI.Services;
using MediaButler.Mobile.Models;
using MediaButler.Shared.UI.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radzen;
using Serilog;
using System.Reflection;

namespace MediaButler.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        builder.Services.AddMauiBlazorWebView();

        // Load appsettings.json configuration
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("MediaButler.Mobile.wwwroot.appsettings.json");

        if (stream != null)
        {
            var config = new ConfigurationBuilder()
                .AddJsonStream(stream)
                .Build();

            builder.Configuration.AddConfiguration(config);
        }

        // M5: Bind FeatureFlags configuration
        builder.Services.Configure<FeatureFlags>(
            builder.Configuration.GetSection("FeatureFlags"));

        // M6: Bind NotificationSettings configuration
        builder.Services.Configure<NotificationSettings>(
            builder.Configuration.GetSection("NotificationSettings"));

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif
        
        builder.Services.AddScoped<DialogService>();
        builder.Services.AddScoped<NotificationService>();
        builder.Services.AddScoped<TooltipService>();
        builder.Services.AddScoped<ContextMenuService>();
        builder.Services.AddScoped<IUtilityServices, UtilityServices>();
        builder.Services.AddSingleton<IHttpsClientHandlerService, HttpsClientHandlerService>();
        builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();

        // M3: New API Services
        // HttpClient with configuration and HTTPS handler
        builder.Services.AddHttpClient<IHttpClientService, HttpClientService>((serviceProvider, client) =>
        {
            var config = serviceProvider.GetRequiredService<IConfigurationService>();
            client.BaseAddress = new Uri(config.ApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(config.ApiTimeout);
            
            if (!string.IsNullOrEmpty(config.ApiKey))
            {
                client.DefaultRequestHeaders.Add("X-Api-Key", config.ApiKey);
            }
        })
        .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
        {
            var httpsHandler = serviceProvider.GetRequiredService<IHttpsClientHandlerService>();
            return httpsHandler.GetPlatformMessageHandler();
        });

        // M5: Memory cache for response caching
        builder.Services.AddMemoryCache();

        // M5: Caching decorator pattern (FilesApiService wrapped with cache)
        builder.Services.AddScoped<FilesApiService>(); // Concrete implementation
        builder.Services.AddScoped<IFilesApiService>(sp =>
        {
            var concrete = sp.GetRequiredService<FilesApiService>();
            var cache = sp.GetRequiredService<IMemoryCache>();
            var logger = sp.GetRequiredService<ILogger<CachedFilesApiService>>();
            var flags = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FeatureFlags>>();

            return new CachedFilesApiService(concrete, cache, logger, flags);
        });

        builder.Services.AddScoped<ITrainingApiService, TrainingApiService>();

        // M6: SSE notification service (Scoped for battery optimization)
        // Connects on demand, disconnects on app background
        builder.Services.AddScoped<ISseNotificationService, SseNotificationService>();

        var _cachePath = FileSystem.Current.CacheDirectory;

        var _appData = FileSystem.AppDataDirectory;

        var logFileName = Path.Combine(_cachePath, "serilog_.log");

        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Debug(restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Debug, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {CorrelationId} {Level:u3} {Username} {Message:lj}{Exception}{NewLine}")
            .WriteTo.File(logFileName, rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 5,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {CorrelationId} {Level:u3} {Username} {Message:lj}{Exception}{NewLine}")
            .CreateLogger();

        builder.Services.AddLogging(logging =>
        {
            logging.AddSerilog(dispose: true);
        });

        // Radzen services
        builder.Services.AddScoped<DialogService>();
        builder.Services.AddScoped<NotificationService>();
        builder.Services.AddScoped<TooltipService>();
        builder.Services.AddScoped<ContextMenuService>();
        
        return builder.Build();
    }
}