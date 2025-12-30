using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using BusLane.Models;

namespace BusLane.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive 
                ? new SolidColorBrush(Color.Parse("#E8F5E9")) 
                : new SolidColorBrush(Color.Parse("#F5F5F5"));
        }
        return new SolidColorBrush(Color.Parse("#F5F5F5"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToStatusColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive 
                ? new SolidColorBrush(Color.Parse("#4CAF50")) 
                : new SolidColorBrush(Color.Parse("#9E9E9E"));
        }
        return new SolidColorBrush(Color.Parse("#9E9E9E"));
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

public class SeverityToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AlertSeverity severity)
        {
            return severity switch
            {
                AlertSeverity.Info => new SolidColorBrush(Color.Parse("#0078D4")),
                AlertSeverity.Warning => new SolidColorBrush(Color.Parse("#FF8C00")),
                AlertSeverity.Critical => new SolidColorBrush(Color.Parse("#D13438")),
                _ => new SolidColorBrush(Color.Parse("#666666"))
            };
        }
        return new SolidColorBrush(Color.Parse("#666666"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class SeverityToBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is AlertSeverity severity)
        {
            return severity switch
            {
                AlertSeverity.Info => new SolidColorBrush(Color.Parse("#E3F2FD")),
                AlertSeverity.Warning => new SolidColorBrush(Color.Parse("#FFF3E0")),
                AlertSeverity.Critical => new SolidColorBrush(Color.Parse("#FFEBEE")),
                _ => new SolidColorBrush(Color.Parse("#F5F5F5"))
            };
        }
        return new SolidColorBrush(Color.Parse("#F5F5F5"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

