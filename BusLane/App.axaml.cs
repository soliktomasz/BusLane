using System;
using System.IO;
using System.Runtime.InteropServices;
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
        SetMacOSDockIcon();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = Program.Services!.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = vm, Title = "Bus Lane"};
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
        }
        catch
        {
            // Silently fail if we can't set the icon
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
