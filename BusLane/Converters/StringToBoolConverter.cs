namespace BusLane.Converters;

using System;
using System.Globalization;
using Avalonia.Data.Converters;

/// <summary>
/// Converts a string to boolean. Returns true when the string is non-null and non-empty, false otherwise.
/// </summary>
public class StringToBoolConverter : IValueConverter
{
    public static StringToBoolConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string str && !string.IsNullOrEmpty(str);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
