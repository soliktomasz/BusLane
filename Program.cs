using Avalonia;
using Avalonia.ReactiveUI;
using System;
using BusLane.Services;
using BusLane.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BusLane;

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
        services.AddSingleton<IVersionService, VersionService>();
        services.AddSingleton<IEncryptionService, EncryptionService>();
        services.AddSingleton<IAzureAuthService, AzureAuthService>();
        services.AddSingleton<IServiceBusService, ServiceBusService>();
        services.AddSingleton<IConnectionStorageService, ConnectionStorageService>();
        services.AddSingleton<IConnectionStringService, ConnectionStringService>();

        // New services for Live Stream, Charts, and Alerts
        services.AddSingleton<ILiveStreamService, LiveStreamService>();
        services.AddSingleton<IMetricsService, MetricsService>();
        services.AddSingleton<IAlertService, AlertService>();
        services.AddSingleton<INotificationService, NotificationService>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<NamespaceViewModel>();
        services.AddTransient<QueueViewModel>();
        services.AddTransient<MessageViewModel>();

        // New ViewModels for Live Stream, Charts, and Alerts
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
