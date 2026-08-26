namespace BusLane.Converters;

using System.Globalization;
using BusLane.Models.Dashboard;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

/// <summary>
/// Converts an <see cref="InboxStatus"/> to a themed brush.
/// Use <c>ConverterParameter=Background</c> for a subtle fill, otherwise a solid foreground color.
/// </summary>
public class InboxStatusToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is InboxStatus status)
        {
            var background = string.Equals(parameter as string, "Background", StringComparison.OrdinalIgnoreCase);
            var resourceKey = (status, background) switch
            {
                (InboxStatus.Critical, true) => "DangerSubtle",
                (InboxStatus.Critical, false) => "TextDanger",
                (InboxStatus.Warning, true) => "WarningSubtle",
                (InboxStatus.Warning, false) => "TextWarning",
                (InboxStatus.Healthy, true) => "SuccessSubtle",
                (InboxStatus.Healthy, false) => "TextSuccess",
                _ => "SubtleForeground"
            };

            if (App.Current?.Resources.TryGetResource(resourceKey, App.Current.ActualThemeVariant, out var resource) == true && resource is SolidColorBrush brush)
            {
                return brush;
            }
        }

        return new SolidColorBrush(Colors.Transparent);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
