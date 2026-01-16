// BusLane/Converters/TabActiveConverter.cs
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace BusLane.Converters;

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

        return Brushes.Transparent;
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

        if (tabId != null && tabId == activeTabId)
        {
            // Try to get the theme-aware resource for accent color
            var app = Application.Current;
            if (app != null)
            {
                var themeVariant = app.ActualThemeVariant;
                if (app.Resources.TryGetResource("AccentBrush", themeVariant, out var resource)
                    && resource is IBrush brush)
                {
                    return brush;
                }
            }
            // Fallback to a default accent blue
            return new SolidColorBrush(Color.Parse("#0078D4"));
        }

        return Brushes.Transparent;
    }
}
