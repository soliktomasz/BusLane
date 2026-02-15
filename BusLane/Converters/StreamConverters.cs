namespace BusLane.Converters;

using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using BusLane.Models;

/// <summary>
/// Converts boolean streaming status to background brush using Fluent 2 semantic colors.
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            // Use Fluent 2 semantic surface colors from theme resources
            var resourceKey = isActive ? "SurfaceSuccess" : "SurfaceSubtle";
            if (App.Current?.Resources.TryGetResource(resourceKey, App.Current.ActualThemeVariant, out var resource) == true && resource is SolidColorBrush brush)
            {
                return brush;
            }
        }
        // Fallback to SurfaceSubtle
        if (App.Current?.Resources.TryGetResource("SurfaceSubtle", App.Current.ActualThemeVariant, out var fallback) == true && fallback is SolidColorBrush fallbackBrush)
        {
            return fallbackBrush;
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean streaming status to indicator color using Fluent 2 semantic colors.
/// </summary>
public class BoolToStatusColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            // Use Fluent 2 semantic text colors for status indicators
            var resourceKey = isActive ? "TextSuccess" : "MutedForeground";
            if (App.Current?.Resources.TryGetResource(resourceKey, App.Current.ActualThemeVariant, out var resource) == true && resource is SolidColorBrush brush)
            {
                return brush;
            }
        }
        // Fallback to MutedForeground
        if (App.Current?.Resources.TryGetResource("MutedForeground", App.Current.ActualThemeVariant, out var fallback) == true && fallback is SolidColorBrush fallbackBrush)
        {
            return fallbackBrush;
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToStatusTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive ? "Streaming" : "Stopped";
        }
        return "Stopped";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToOpacityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // For IsAcknowledged: true = 0.5 (dimmed), false = 1.0 (full)
            // For IsEnabled: true = 1.0 (full), false = 0.5 (dimmed)
            return boolValue ? 0.5 : 1.0;
        }
        return 1.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts AlertSeverity to foreground color using Fluent 2 semantic colors.
/// </summary>
public class SeverityToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AlertSeverity severity)
        {
            string resourceKey = severity switch
            {
                AlertSeverity.Info => "AccentBrand",
                AlertSeverity.Warning => "TextWarning",
                AlertSeverity.Critical => "TextDanger",
                _ => "SubtleForeground"
            };
            
            if (App.Current?.Resources.TryGetResource(resourceKey, App.Current.ActualThemeVariant, out var resource) == true && resource is SolidColorBrush brush)
            {
                return brush;
            }
        }
        
        // Fallback to SubtleForeground
        if (App.Current?.Resources.TryGetResource("SubtleForeground", App.Current.ActualThemeVariant, out var fallback) == true && fallback is SolidColorBrush fallbackBrush)
        {
            return fallbackBrush;
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts AlertSeverity to background color using Fluent 2 semantic surface colors.
/// </summary>
public class SeverityToBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AlertSeverity severity)
        {
            string resourceKey = severity switch
            {
                AlertSeverity.Info => "SurfaceBrand",
                AlertSeverity.Warning => "SurfaceWarning",
                AlertSeverity.Critical => "SurfaceDanger",
                _ => "SurfaceSubtle"
            };
            
            if (App.Current?.Resources.TryGetResource(resourceKey, App.Current.ActualThemeVariant, out var resource) == true && resource is SolidColorBrush brush)
            {
                return brush;
            }
        }
        
        // Fallback to SurfaceSubtle
        if (App.Current?.Resources.TryGetResource("SurfaceSubtle", App.Current.ActualThemeVariant, out var fallback) == true && fallback is SolidColorBrush fallbackBrush)
        {
            return fallbackBrush;
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

