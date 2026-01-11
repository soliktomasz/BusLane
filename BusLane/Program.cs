namespace BusLane;

using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;

using Services.Abstractions;
using Services.Auth;
using Services.Infrastructure;
using Services.Monitoring;
using Services.ServiceBus;
using Services.Storage;
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

        // Get Sentry DSN from configuration or environment variable
        var sentryDsn = configuration["Sentry:Dsn"] 
                        ?? Environment.GetEnvironmentVariable("SENTRY_DSN") 
                        ?? "";
        
        // Initialize Sentry SDK
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
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IKeyboardShortcutService, KeyboardShortcutService>();
        
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

        // Main ViewModel with unified services
        services.AddSingleton<MainWindowViewModel>(sp => new MainWindowViewModel(
            sp.GetRequiredService<IAzureAuthService>(),
            sp.GetRequiredService<IAzureResourceService>(),
            sp.GetRequiredService<IServiceBusOperationsFactory>(),
            sp.GetRequiredService<IConnectionStorageService>(),
            sp.GetRequiredService<IVersionService>(),
            sp.GetRequiredService<IPreferencesService>(),
            sp.GetRequiredService<ILiveStreamService>(),
            sp.GetRequiredService<IMetricsService>(),
            sp.GetRequiredService<IAlertService>(),
            sp.GetRequiredService<INotificationService>(),
            sp.GetRequiredService<IKeyboardShortcutService>()
        ));

        // Other ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<NamespaceViewModel>();
        services.AddTransient<QueueViewModel>();
        services.AddTransient<MessageViewModel>();
        services.AddTransient<TopicViewModel>();

        // ViewModels for Live Stream, Charts, and Alerts
        services.AddTransient<LiveStreamViewModel>();
        services.AddTransient<ChartsViewModel>();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
