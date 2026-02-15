namespace BusLane.Converters;

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using BusLane.Models.Logging;

/// <summary>
/// Converts LogSource enum to display strings.
/// </summary>
public class LogSourceToStringConverter : IValueConverter
{
    public static LogSourceToStringConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            LogSource.Application => "APP",
            LogSource.ServiceBus => "SB",
            _ => "APP"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
