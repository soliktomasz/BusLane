namespace BusLane;

using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using BusLane.Services.Abstractions;
using BusLane.ViewModels;
using BusLane.Views;
using Microsoft.Extensions.DependencyInjection;

public partial class App : Application
{
    public static App? Instance { get; private set; }
    
    /// <summary>
    /// Gets the main window (used for file dialogs).
    /// </summary>
    public static Window? MainWindow { get; private set; }
    
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
            ApplyTheme(preferencesService.Theme);
            
            var vm = Program.Services!.GetRequiredService<MainWindowViewModel>();
            var mainWindow = new MainWindow { DataContext = vm, Title = "BusLane" };
            MainWindow = mainWindow;
            
            // Set up file dialog service now that we have the window
            var fileDialogService = new FileDialogService(() => MainWindow);
            vm.SetFileDialogService(fileDialogService);
            
            desktop.MainWindow = mainWindow;
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

            // Set the application name in the menu bar
            SetMacOSMenuBarTitle(sharedApp);
        }
        catch
        {
            // Silently fail if we can't set the icon
        }
    }

    private static void SetMacOSMenuBarTitle(IntPtr nsApp)
    {
        try
        {
            // Get the main menu
            var mainMenuSelector = sel_registerName("mainMenu");
            var mainMenu = objc_msgSend(nsApp, mainMenuSelector);

            if (mainMenu != IntPtr.Zero)
            {
                // Get the first item (application menu)
                var itemAtIndexSelector = sel_registerName("itemAtIndex:");
                var appMenuItem = objc_msgSend(mainMenu, itemAtIndexSelector, (IntPtr)0);

                if (appMenuItem != IntPtr.Zero)
                {
                    // Get the submenu
                    var submenuSelector = sel_registerName("submenu");
                    var appMenu = objc_msgSend(appMenuItem, submenuSelector);

                    if (appMenu != IntPtr.Zero)
                    {
                        // Set the title of the first menu item to "BusLane"
                        var setTitleSelector = sel_registerName("setTitle:");
                        var nsString = objc_getClass("NSString");
                        var stringWithUTF8String = sel_registerName("stringWithUTF8String:");
                        var titleString = objc_msgSend(nsString, stringWithUTF8String, "BusLane");

                        // Get the first item in the submenu (About item)
                        var firstItem = objc_msgSend(appMenu, itemAtIndexSelector, (IntPtr)0);
                        if (firstItem != IntPtr.Zero)
                        {
                            objc_msgSend(firstItem, setTitleSelector, titleString);
                        }
                    }
                }
            }
        }
        catch
        {
            // Silently fail if we can't set the menu title
        }
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
