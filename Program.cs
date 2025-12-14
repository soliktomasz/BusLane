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
        services.AddSingleton<IAzureAuthService, AzureAuthService>();
        services.AddSingleton<IServiceBusService, ServiceBusService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<NamespaceViewModel>();
        services.AddTransient<QueueViewModel>();
        services.AddTransient<MessageViewModel>();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}
