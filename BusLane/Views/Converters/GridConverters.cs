namespace BusLane.Views.Converters;

using Avalonia.Data.Converters;
using System.Globalization;

/// <summary>
/// Converts a grid column span to a pixel width proportional to available container width.
/// Uses a 12-column grid system. The converter parameter should be bound to the container width.
/// Falls back to 80px per unit if no container width is available.
/// </summary>
public class GridColumnToWidthConverter : IMultiValueConverter
{
    private const int GridColumns = 12;
    private const double Gap = 8;
    private const double FallbackUnitSize = 80;

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var columnSpan = values.Count > 0 && values[0] is int span ? span : 6;
        var containerWidth = values.Count > 1 && values[1] is double width && width > 0 ? width : 0;

        if (containerWidth > 0)
        {
            var unitWidth = (containerWidth - Gap) / GridColumns;
            return columnSpan * unitWidth - Gap;
        }

        return columnSpan * FallbackUnitSize - Gap;
    }
}

/// <summary>
/// Converts a grid row span to a pixel height.
/// Uses a fixed row height since vertical space is scrollable.
/// </summary>
public class GridRowToHeightConverter : IValueConverter
{
    private const double RowHeight = 80;
    private const double Gap = 8;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int height)
            return height * RowHeight - Gap;
        return 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
