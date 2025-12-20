using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using BusLane.ViewModels;
using BusLane.Views;
using Microsoft.Extensions.DependencyInjection;

namespace BusLane;

public partial class App : Application
{
    public static App? Instance { get; private set; }
    
    public override void Initialize()
    {
        Instance = this;
        AvaloniaXamlLoader.Load(this);
        ApplyTheme(Preferences.Theme);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = Program.Services!.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = vm };
        }
        base.OnFrameworkInitializationCompleted();
    }
    
    public void ApplyTheme(string themeName)
    {
        RequestedThemeVariant = themeName switch
        {
            "Dark" => ThemeVariant.Dark,
            "Light" => ThemeVariant.Light,
            "System" or _ => ThemeVariant.Default
        };
    }
}
