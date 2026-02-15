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
/// Converts EntityType to a Lucide icon name string.
/// </summary>
public class EntityTypeToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is EntityType type)
        {
            return type switch
            {
                EntityType.Queue => "Inbox",
                EntityType.Topic => "Send",
                EntityType.Subscription => "BookOpen",
                _ => "Box"
            };
        }
        return "Box";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Compares a string value to a parameter and returns true if they are equal.
/// Used for RadioButton binding where IsChecked should be true when value matches parameter.
/// </summary>
public class StringEqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string strValue && parameter is string strParam)
        {
            return strValue.Equals(strParam, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // When RadioButton is checked (true), return the parameter as the new value
        if (value is true && parameter is string strParam)
        {
            return strParam;
        }
        return Avalonia.Data.BindingOperations.DoNothing;
    }
}


