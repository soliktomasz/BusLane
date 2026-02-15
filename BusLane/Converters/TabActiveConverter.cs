namespace BusLane.Converters;

// BusLane/Converters/TabActiveConverter.cs
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

/// <summary>
/// Converts tab active state to background brush.
/// Compares TabId with ActiveTab.TabId to determine active state.
/// </summary>
public class TabActiveConverter : IMultiValueConverter
{
    public static readonly TabActiveConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return Brushes.Transparent;

        var tabId = values[0] as string;
        var activeTabId = values[1] as string;

        if (tabId != null && tabId == activeTabId)
        {
            // Try to get the theme-aware resource for selected background
            var app = Application.Current;
            if (app != null)
            {
                var themeVariant = app.ActualThemeVariant;
                if (app.Resources.TryGetResource("SelectedBackground", themeVariant, out var resource)
                    && resource is IBrush brush)
                {
                    return brush;
                }
            }
            // Fallback
            return new SolidColorBrush(Color.Parse("#E6F2FB"));
        }

        // Inactive tab - use card background for visibility
        var appRef = Application.Current;
        if (appRef != null)
        {
            var themeVariant = appRef.ActualThemeVariant;
            if (appRef.Resources.TryGetResource("CardBackground", themeVariant, out var cardResource)
                && cardResource is IBrush cardBrush)
            {
                return cardBrush;
            }
        }
        return new SolidColorBrush(Color.Parse("#FFFFFF"));
    }
}

/// <summary>
/// Converts tab active state to border brush.
/// Active tabs show accent color border at bottom.
/// </summary>
public class TabActiveBorderConverter : IMultiValueConverter
{
    public static readonly TabActiveBorderConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return Brushes.Transparent;

        var tabId = values[0] as string;
        var activeTabId = values[1] as string;

        var app = Application.Current;
        if (app != null)
        {
            var themeVariant = app.ActualThemeVariant;
            
            if (tabId != null && tabId == activeTabId)
            {
                // Active tab - use accent brand color
                if (app.Resources.TryGetResource("AccentBrand", themeVariant, out var accentResource)
                    && accentResource is IBrush accentBrush)
                {
                    return accentBrush;
                }
                // Fallback to a default accent blue
                return new SolidColorBrush(Color.Parse("#0078D4"));
            }
            
            // Inactive tab - use default border color
            if (app.Resources.TryGetResource("BorderDefault", themeVariant, out var borderResource)
                && borderResource is IBrush borderBrush)
            {
                return borderBrush;
            }
        }
        
        return new SolidColorBrush(Color.Parse("#E0E0E0"));
    }
}

/// <summary>
/// Converts tab active state to text foreground brush.
/// Active tabs show accent color text.
/// </summary>
public class TabActiveForegroundConverter : IMultiValueConverter
{
    public static readonly TabActiveForegroundConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return Brushes.Transparent;

        var tabId = values[0] as string;
        var activeTabId = values[1] as string;

        var app = Application.Current;
        if (app != null)
        {
            var themeVariant = app.ActualThemeVariant;
            
            if (tabId != null && tabId == activeTabId)
            {
                // Active tab - use accent brand color for text
                if (app.Resources.TryGetResource("AccentBrand", themeVariant, out var accentResource)
                    && accentResource is IBrush accentBrush)
                {
                    return accentBrush;
                }
                return new SolidColorBrush(Color.Parse("#0078D4"));
            }
            
            // Inactive tab - use default foreground
            if (app.Resources.TryGetResource("AppForeground", themeVariant, out var fgResource)
                && fgResource is IBrush fgBrush)
            {
                return fgBrush;
            }
        }
        
        return new SolidColorBrush(Color.Parse("#242424"));
    }
}

/// <summary>
/// Converts tab active state to font weight.
/// Active tabs show bold text.
/// </summary>
public class TabActiveFontWeightConverter : IMultiValueConverter
{
    public static readonly TabActiveFontWeightConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return FontWeight.SemiBold;

        var tabId = values[0] as string;
        var activeTabId = values[1] as string;

        if (tabId != null && tabId == activeTabId)
        {
            return FontWeight.Bold;
        }

        return FontWeight.SemiBold;
    }
}

/// <summary>
/// Converts tab active state to box shadow for elevation effect.
/// Active tabs have elevation (shadow) to appear raised.
/// </summary>
public class TabActiveElevationConverter : IMultiValueConverter
{
    public static readonly TabActiveElevationConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return new BoxShadows();

        var tabId = values[0] as string;
        var activeTabId = values[1] as string;

        if (tabId != null && tabId == activeTabId)
        {
            // Active tab - add elevation shadow
            return new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 2,
                Blur = 4,
                Spread = 0,
                Color = Color.Parse("#20000000")
            });
        }

        // Inactive tab - no shadow
        return new BoxShadows();
    }
}
