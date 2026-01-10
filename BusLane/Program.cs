using Avalonia;
using Avalonia.ReactiveUI;
using BusLane.Services.Abstractions;
using BusLane.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BusLane;

using Services.Auth;
using Services.Infrastructure;
using Services.Monitoring;
using Services.ServiceBus;
using Services.Storage;

class Program
{
    public static IServiceProvider? Services { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
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
            return new ServiceBusOperationsFactory(auth.ArmClient);
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
