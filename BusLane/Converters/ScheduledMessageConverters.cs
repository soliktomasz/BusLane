namespace BusLane.Converters;

using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

/// <summary>
/// Presentation helpers for the local scheduled-message states produced by ScheduledMessageManagementService.
/// </summary>
public static class ScheduledMessageConverters
{
    /// <summary>Short state name: "Due / unverified (local)" becomes "Due".</summary>
    public static readonly FuncValueConverter<string?, string> StateLabel =
        new(state => state is null ? "" : state.Split(" (")[0].Split(" / ")[0]);

    /// <summary>" · Production" style suffix; empty when no environment is set.</summary>
    public static readonly FuncValueConverter<Models.ConnectionEnvironment, string> EnvironmentSuffix =
        new(env => env == Models.ConnectionEnvironment.None ? "" : $" · {env}");
}

/// <summary>
/// Converts a local scheduled-message state to a semantic brush. Pass "Background" as the parameter for the surface tint.
/// </summary>
public class ScheduledStateToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var background = parameter as string == "Background";
        var state = value as string ?? "";
        var resourceKey = state switch
        {
            _ when state.StartsWith("Upcoming") => background ? "SurfaceBrand" : "AccentBrand",
            _ when state.StartsWith("Rescheduled") => background ? "SurfaceSuccess" : "TextSuccess",
            _ when state.StartsWith("Action failed") => background ? "SurfaceDanger" : "TextDanger",
            _ when state.StartsWith("Due") || state.StartsWith("Limited") => background ? "SurfaceWarning" : "TextWarning",
            _ => background ? "SurfaceSubtle" : "SubtleForeground"
        };

        if (App.Current?.Resources.TryGetResource(resourceKey, App.Current.ActualThemeVariant, out var resource) == true && resource is SolidColorBrush brush)
        {
            return brush;
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
