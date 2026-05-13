using MediaButler.API.Modules.RealTime.Services;

namespace MediaButler.API.Configuration;

public static class RealTimeModuleExtensions
{
    /// <summary>
    /// Registers the Real-Time Communication Module (SignalR) services.
    /// </summary>
    public static IServiceCollection AddRealTimeModule(this IServiceCollection services)
    {
        // Register the central real-time service
        services.AddSingleton<IRealTimeService, RealTimeService>();
        
        // Note: SignalR Hubs are registered automatically by ASP.NET Core logic but mapped in Program.cs.
        // If we wanted to self-contain hub mapping, we'd need an endpoint route builder extension too.
        // For now, we just register the services.
        
        return services;
    }
}
