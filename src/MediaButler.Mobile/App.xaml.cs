using Serilog;

namespace MediaButler.Mobile;

public partial class App : Application
{
    // Track app state for components to react
    public static bool IsInBackground { get; private set; } = false;

    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) { Title = "MediaButler.Mobile" };
    }

    // M6: MAUI Lifecycle - Battery Optimization
    // SignalR service is Scoped, managed by components
    // These hooks track app state for components to react

    protected override void OnStart()
    {
        base.OnStart();
        IsInBackground = false;
        Log.Information("📱 App started (foreground)");
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        IsInBackground = true;
        Log.Information("💤 App sleeping (background) - SignalR connections should stop");

        // Note: SignalR service is Scoped, components will handle disconnection
        // This just tracks state for components to check
    }

    protected override void OnResume()
    {
        base.OnResume();
        IsInBackground = false;
        Log.Information("📱 App resumed (foreground) - SignalR connections can restart");

        // Note: Components will re-establish SignalR connections as needed
    }
}