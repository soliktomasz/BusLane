namespace BusLane.Converters;

using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using BusLane.Models.Dashboard;

/// <summary>
/// Converts MetricTrend to an arrow symbol.
/// </summary>
public class TrendToArrowConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MetricTrend trend)
        {
            return trend switch
            {
                MetricTrend.Up => "↑",
                MetricTrend.Down => "↓",
                _ => "→"
            };
        }
        return "→";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts MetricTrend to a color brush using Fluent 2 semantic colors.
/// </summary>
public class TrendToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is MetricTrend trend)
        {
            string resourceKey = trend switch
            {
                MetricTrend.Up => "TextSuccess",
                MetricTrend.Down => "TextDanger",
                _ => "MutedForeground"
            };

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

/// <summary>
/// Converts EntityType to an icon symbol.
/// </summary>
public class EntityTypeToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is EntityType type)
        {
            return type switch
            {
                EntityType.Queue => "📥",
                EntityType.Topic => "📤",
                EntityType.Subscription => "📋",
                _ => "📦"
            };
        }
        return "📦";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
