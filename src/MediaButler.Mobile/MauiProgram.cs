using MediaButler.Mobile.Components.Interface;
using MediaButler.Mobile.Components.Interfaces;
using MediaButler.Mobile.Components.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Radzen;
using Serilog;

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

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif
        
        builder.Services.AddScoped<DialogService>();
        builder.Services.AddScoped<NotificationService>();
        builder.Services.AddScoped<TooltipService>();
        builder.Services.AddScoped<ContextMenuService>();
        builder.Services.AddScoped<IUtilityServices, UtilityServices>();
        builder.Services.AddScoped<IServiceApi, ServiceApi>();
        builder.Services.AddSingleton<IHttpsClientHandlerService, HttpsClientHandlerService>();
        builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();

        // M3: New API Services
        // HttpClient with configuration and HTTPS handler
        builder.Services.AddHttpClient<IHttpClientService, HttpClientService>((serviceProvider, client) =>
        {
            var config = serviceProvider.GetRequiredService<IConfigurationService>();
            client.BaseAddress = new Uri(config.ApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(config.ApiTimeout);
        })
        .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
        {
            var httpsHandler = serviceProvider.GetRequiredService<IHttpsClientHandlerService>();
            return httpsHandler.GetPlatformMessageHandler();
        });

        // TODO M4: Re-enable after DTO alignment
        // builder.Services.AddScoped<IFilesApiService, FilesApiService>();
        builder.Services.AddScoped<ITrainingApiService, TrainingApiService>();

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