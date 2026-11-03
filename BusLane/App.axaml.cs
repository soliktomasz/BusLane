namespace BusLane;

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using BusLane.Models.Update;
using BusLane.Services.Abstractions;
using BusLane.Services.Infrastructure;
using BusLane.Services.Update;
using BusLane.Styling;
using BusLane.ViewModels;
using BusLane.Views;
using BusLane.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

public partial class App : Application
{
    private const string AboutDescription = "A desktop workspace for Azure Service Bus operations and troubleshooting.";
    private const string GitHubRepositoryUrl = "https://github.com/soliktomasz/BusLane";

    public static App? Instance { get; private set; }
    
    /// <summary>
    /// Gets the main window (used for file dialogs).
    /// </summary>
    public static Window? MainWindow { get; private set; }

    private string _themeName = "Light";
    private string _designName = AppThemes.ClassicDesign;
    private IStyle? _zedStyles;
    private (Color Light, Color Dark)? _classicAccents;
    
    public override void Initialize()
    {
        Instance = this;
        AvaloniaXamlLoader.Load(this);
        // Theme will be applied in OnFrameworkInitializationCompleted when services are available
        SetMacOSDockIcon();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Apply theme from preferences service
            var preferencesService = Program.Services!.GetRequiredService<IPreferencesService>();
            ApplyTheme(preferencesService.Theme, preferencesService.Design);
            // "System" under the Zed design picks a Zed variant itself, so it has to follow OS changes
            if (PlatformSettings is not null)
                PlatformSettings.ColorValuesChanged += (_, _) => ApplyTheme(_themeName, _designName);
            
            var vm = Program.Services!.GetRequiredService<MainWindowViewModel>();
            var mainWindow = new MainWindow { DataContext = vm, Title = "BusLane" };
            MainWindow = mainWindow;
            
            // Set up file dialog service now that we have the window
            var fileDialogService = new FileDialogService(() => MainWindow);
            vm.SetFileDialogService(fileDialogService);
            
            desktop.MainWindow = mainWindow;
            ConfigureMacOSMainMenu(mainWindow, vm);
        }
        base.OnFrameworkInitializationCompleted();
    }
    
    public void ApplyTheme(string themeName, string designName)
    {
        _themeName = themeName;
        _designName = designName;

        var systemIsDark = PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark;
        RequestedThemeVariant = AppThemes.Resolve(themeName, designName, systemIsDark);

        var useZed = designName == AppThemes.ZedDesign;
        if (useZed)
        {
            _zedStyles ??= (IStyle)AvaloniaXamlLoader.Load(new Uri(AppThemes.ZedStylesUri));
            if (!Styles.Contains(_zedStyles))
                Styles.Add(_zedStyles);
        }
        else if (_zedStyles is not null)
        {
            Styles.Remove(_zedStyles);
        }

        if (Styles.OfType<FluentTheme>().FirstOrDefault() is { } fluent)
        {
            var light = fluent.Palettes[ThemeVariant.Light];
            var dark = fluent.Palettes[ThemeVariant.Dark];
            _classicAccents ??= (light.Accent, dark.Accent);
            light.Accent = useZed ? AppThemes.ZedLightAccent : _classicAccents.Value.Light;
            dark.Accent = useZed ? AppThemes.ZedDarkAccent : _classicAccents.Value.Dark;
        }
    }
    
    private static void SetMacOSDockIcon()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "icon.icns");
            if (!File.Exists(iconPath))
                return;

            var nsImage = objc_getClass("NSImage");
            var alloc = sel_registerName("alloc");
            var initWithContentsOfFile = sel_registerName("initWithContentsOfFile:");
            var setApplicationIconImage = sel_registerName("setApplicationIconImage:");
            var sharedApplication = sel_registerName("sharedApplication");

            var nsApp = objc_getClass("NSApplication");
            var sharedApp = objc_msgSend(nsApp, sharedApplication);

            var nsString = objc_getClass("NSString");
            var stringWithUTF8String = sel_registerName("stringWithUTF8String:");
            var pathString = objc_msgSend(nsString, stringWithUTF8String, iconPath);

            var imageAlloc = objc_msgSend(nsImage, alloc);
            var image = objc_msgSend(imageAlloc, initWithContentsOfFile, pathString);

            if (image != IntPtr.Zero)
            {
                objc_msgSend(sharedApp, setApplicationIconImage, image);
            }
        }
        catch
        {
            // Silently fail if we can't set the icon
        }
    }

    private static void ConfigureMacOSMainMenu(Window mainWindow, MainWindowViewModel viewModel)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        var menu = CreateMacMenu(
            (_, _) => ToggleFullscreen(mainWindow),
            (_, _) => ShowLogViewer(viewModel),
            (_, _) => ShowTerminal(viewModel),
            (_, _) => viewModel.ShowIntroductionSplashCommand.Execute(null),
            async (sender, _) => await RunCheckForUpdatesMenuActionAsync(
                sender as NativeMenuItem,
                CheckForUpdatesFromMenuAsync),
            async (_, _) => await ShowAboutDialogAsync(mainWindow));
        NativeMenu.SetMenu(mainWindow, menu);
    }

    internal static NativeMenu CreateMacMenu(
        EventHandler onToggleFullscreenClick,
        EventHandler onShowLogViewerClick,
        EventHandler onShowTerminalClick,
        EventHandler onShowIntroductionClick,
        EventHandler onCheckForUpdatesClick,
        EventHandler onAboutClick)
    {
        var toggleFullscreenItem = new NativeMenuItem("Toggle Fullscreen");
        toggleFullscreenItem.Click += onToggleFullscreenClick;

        var showLogViewerItem = new NativeMenuItem("Show Log Viewer");
        showLogViewerItem.Click += onShowLogViewerClick;

        var showTerminalItem = new NativeMenuItem("Show Terminal");
        showTerminalItem.Click += onShowTerminalClick;

        var viewMenu = new NativeMenu();
        viewMenu.Add(toggleFullscreenItem);
        viewMenu.Add(showLogViewerItem);
        viewMenu.Add(showTerminalItem);

        var aboutItem = new NativeMenuItem("About BusLane");
        aboutItem.Click += onAboutClick;

        var showIntroductionItem = new NativeMenuItem("Show Introduction");
        showIntroductionItem.Click += onShowIntroductionClick;

        var checkForUpdatesItem = new NativeMenuItem("Check for Updates...");
        checkForUpdatesItem.Click += onCheckForUpdatesClick;

        var helpMenu = new NativeMenu();
        helpMenu.Add(aboutItem);
        helpMenu.Add(showIntroductionItem);
        helpMenu.Add(checkForUpdatesItem);

        var rootMenu = new NativeMenu();
        rootMenu.Add(new NativeMenuItem("View")
        {
            Menu = viewMenu
        });
        rootMenu.Add(new NativeMenuItem("Help")
        {
            Menu = helpMenu
        });

        return rootMenu;
    }

    private static void ToggleFullscreen(Window mainWindow)
    {
        mainWindow.WindowState = mainWindow.WindowState == WindowState.FullScreen
            ? WindowState.Normal
            : WindowState.FullScreen;
    }

    private static void ShowLogViewer(MainWindowViewModel viewModel) => viewModel.LogViewer.Open();
    private static void ShowTerminal(MainWindowViewModel viewModel) => viewModel.Terminal.Open();

    internal static async Task RunCheckForUpdatesMenuActionAsync(NativeMenuItem? menuItem, Func<Task<string>> checkForUpdatesAsync)
    {
        if (menuItem != null)
        {
            menuItem.Header = "Checking for Updates...";
            menuItem.IsEnabled = false;
        }

        try
        {
            var statusMessage = await checkForUpdatesAsync();
            if (menuItem != null)
            {
                menuItem.Header = string.IsNullOrWhiteSpace(statusMessage)
                    ? "Check for Updates..."
                    : statusMessage;
            }
        }
        catch (Exception)
        {
            if (menuItem != null)
            {
                menuItem.Header = "Check for Updates... (error)";
            }
        }
        finally
        {
            if (menuItem != null)
            {
                menuItem.IsEnabled = true;
            }
        }
    }

    private static async Task<string> CheckForUpdatesFromMenuAsync()
    {
        var updateService = Program.Services?.GetService<IUpdateService>();
        if (updateService == null)
            return "Update service is unavailable.";

        await updateService.CheckForUpdatesAsync(manualCheck: true);
        if (updateService.Status == UpdateStatus.UpToDate)
            return "No new updates";

        return updateService.StatusMessage;
    }

    private static async Task ShowAboutDialogAsync(Window owner)
    {
        var versionService = Program.Services?.GetService<IVersionService>();
        var version = versionService?.DisplayVersion ?? "v0.0.0";
        var dialog = new AboutDialog(version, AboutDescription, GitHubRepositoryUrl);
        await dialog.ShowDialog(owner);
    }

    private async void AboutMenuItem_OnClick(object? sender, EventArgs args)
    {
        if (MainWindow is null)
            return;

        await ShowAboutDialogAsync(MainWindow);
    }

    private void ShowIntroductionMenuItem_OnClick(object? sender, EventArgs args)
    {
        if (MainWindow?.DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ShowIntroductionSplashCommand.Execute(null);
        }
    }

    private async void CheckForUpdatesMenuItem_OnClick(object? sender, EventArgs args)
    {
        await RunCheckForUpdatesMenuActionAsync(sender as NativeMenuItem, CheckForUpdatesFromMenuAsync);
    }

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string className);
    
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string selectorName);
    
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);
    
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);
    
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, string arg1);
}
