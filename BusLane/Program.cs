namespace BusLane;

using System.Text.Json;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Models.Logging;
using Serilog;
using Serilog.Events;

using Services.Abstractions;
using Services.Auth;
using Services.Infrastructure;
using Services.Monitoring;
using Services.ServiceBus;
using Services.Storage;
using Services.Dashboard;
using Services.Update;
using ViewModels;

class Program
{
    public static IServiceProvider? Services { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        // Build configuration from appsettings files
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        // Check if telemetry is enabled in user preferences
        var telemetryEnabled = IsTelemetryEnabled();

        // Get Sentry DSN from configuration or environment variable
        var sentryDsn = telemetryEnabled
            ? configuration["Sentry:Dsn"]
              ?? Environment.GetEnvironmentVariable("SENTRY_DSN")
              ?? ""
            : "";

        // Initialize Sentry SDK only if telemetry is enabled
        using var sentryDisposable = SentrySdk.Init(o =>
        {
            o.Dsn = sentryDsn;
            o.Debug = false;
            o.TracesSampleRate = 1.0;
            o.IsGlobalModeEnabled = true;
            o.AutoSessionTracking = true;
            o.AttachStacktrace = true;
        });

        // Configure Serilog with Sentry sink
        ConfigureLogging(sentryDsn);
        
        try
        {
            Log.Information("Starting BusLane application");
            
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            SentrySdk.CaptureException(ex);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void ConfigureLogging(string sentryDsn = "")
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BusLane",
            "logs",
            "buslane-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Sentry(o =>
            {
                o.Dsn = sentryDsn;
                o.MinimumBreadcrumbLevel = LogEventLevel.Debug;
                o.MinimumEventLevel = LogEventLevel.Error;
                o.AttachStacktrace = true;
                o.SendDefaultPii = false;
            })
            .CreateLogger();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Infrastructure services
        services.AddSingleton<IVersionService, VersionService>();
        services.AddSingleton<IEncryptionService, EncryptionService>();
        services.AddSingleton<IPreferencesService, PreferencesService>();
        services.AddSingleton<IKeyboardShortcutService, KeyboardShortcutService>();
        services.AddSingleton<ILogSink, LogSink>();
        
        // Auth services
        services.AddSingleton<IAzureAuthService, AzureAuthService>();
        
        // Service Bus services - unified operations
        services.AddSingleton<IServiceBusOperationsFactory>(sp =>
        {
            var auth = sp.GetRequiredService<IAzureAuthService>();
            return new ServiceBusOperationsFactory(() => auth.ArmClient);
        });
        services.AddSingleton<IAzureResourceService, AzureResourceService>();
        services.AddSingleton<IConnectionStorageService, ConnectionStorageService>();

        // Monitoring services for Live Stream, Charts, and Alerts
        services.AddSingleton<ILiveStreamService, LiveStreamService>();
        services.AddSingleton<IMetricsService, MetricsService>();
        services.AddSingleton<IAlertService, AlertService>();
        services.AddSingleton<INotificationService, NotificationService>();

        // Update services
        services.AddSingleton<UpdateDownloadService>();
        services.AddSingleton<IUpdateService, UpdateService>();

        // Dashboard services
        services.AddSingleton<IDashboardPersistenceService, DashboardPersistenceService>();
        services.AddSingleton<DashboardLayoutEngine>();
        services.AddSingleton<ViewModels.Dashboard.DashboardViewModel>();

        // Add dashboard services
        services.AddSingleton<IDashboardRefreshService, DashboardRefreshService>();
        services.AddTransient<ViewModels.Dashboard.NamespaceDashboardViewModel>();

        // Main ViewModel with unified services
        services.AddSingleton<MainWindowViewModel>(sp => new MainWindowViewModel(
            sp.GetRequiredService<IAzureAuthService>(),
            sp.GetRequiredService<IAzureResourceService>(),
            sp.GetRequiredService<IServiceBusOperationsFactory>(),
            sp.GetRequiredService<IConnectionStorageService>(),
            sp.GetRequiredService<IVersionService>(),
            sp.GetRequiredService<IPreferencesService>(),
            sp.GetRequiredService<ILiveStreamService>(),
            sp.GetRequiredService<IAlertService>(),
            sp.GetRequiredService<INotificationService>(),
            sp.GetRequiredService<IKeyboardShortcutService>(),
            sp.GetRequiredService<IUpdateService>(),
            sp.GetRequiredService<ILogSink>(),
            sp.GetRequiredService<ViewModels.Dashboard.DashboardViewModel>(),
            sp.GetRequiredService<ViewModels.Dashboard.NamespaceDashboardViewModel>()
        ));

    }

    /// <summary>
    /// Reads the telemetry preference directly from the preferences file.
    /// This runs before DI is available, so we read the JSON file manually.
    /// </summary>
    private static bool IsTelemetryEnabled()
    {
        try
        {
            var prefsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BusLane",
                "preferences.json");

            if (!File.Exists(prefsPath))
                return false; // Default: disabled (opt-in)

            var json = File.ReadAllText(prefsPath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("EnableTelemetry", out var prop))
                return prop.GetBoolean();
        }
        catch
        {
            // If anything fails, default to disabled (opt-in)
        }

        return false;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
