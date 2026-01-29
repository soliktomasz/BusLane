using System;
using System.Globalization;
using Avalonia.Data.Converters;
using BusLane.Models.Logging;

namespace BusLane.Converters;

/// <summary>
/// Converts LogLevel enum to boolean for conditional class assignment.
/// Use with ConverterParameter to specify which level to match.
/// </summary>
public class LogLevelToBoolConverter : IValueConverter
{
    public static LogLevelToBoolConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not LogLevel level || parameter is not string levelName)
            return false;

        return levelName.ToUpperInvariant() switch
        {
            "INFO" => level == LogLevel.Info,
            "WARNING" => level == LogLevel.Warning,
            "ERROR" => level == LogLevel.Error,
            "DEBUG" => level == LogLevel.Debug,
            _ => false
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
