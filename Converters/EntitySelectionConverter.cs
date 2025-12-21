using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace BusLane.Converters;

/// <summary>
/// Converts entity selection state to a boolean by comparing the current item with the selected item.
/// Used in multi-binding to determine if an entity is currently selected.
/// </summary>
public class EntitySelectionConverter : IMultiValueConverter
{
    public static readonly EntitySelectionConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2)
            return false;

        var currentItem = values[0];
        var selectedItem = values[1];

        if (currentItem == null || selectedItem == null)
            return false;

        return ReferenceEquals(currentItem, selectedItem) || currentItem.Equals(selectedItem);
    }
}

/// <summary>
/// Converter for dead letter badge visibility that combines the count and the setting.
/// Values[0] = DeadLetterCount (long), Values[1] = ShowDeadLetterBadges (bool)
/// </summary>
public class DeadLetterBadgeVisibilityConverter : IMultiValueConverter
{
    public static readonly DeadLetterBadgeVisibilityConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2)
            return false;

        // First value is the dead letter count
        var hasDeadLetters = values[0] switch
        {
            long count => count > 0,
            int count => count > 0,
            _ => false
        };

        // Second value is the ShowDeadLetterBadges setting
        var showBadges = values[1] switch
        {
            bool show => show,
            _ => true // Default to showing if setting is not available
        };

        return hasDeadLetters && showBadges;
    }
}

/// <summary>
/// Converts entity selection state to a background brush.
/// Returns the selected background color when selected, transparent otherwise.
/// </summary>
public class EntitySelectionBackgroundConverter : IMultiValueConverter
{
    public static readonly EntitySelectionBackgroundConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2)
            return Brushes.Transparent;

        var currentItem = values[0];
        var selectedItem = values[1];

        if (currentItem == null || selectedItem == null)
            return Brushes.Transparent;

        var isSelected = ReferenceEquals(currentItem, selectedItem) || currentItem.Equals(selectedItem);
        
        if (isSelected)
        {
            // Try to get the theme-aware resource
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
            // Fallback for light theme
            return new SolidColorBrush(Color.Parse("#E6F2FB"));
        }
        
        return Brushes.Transparent;
    }
}

