namespace BusLane.Views.Controls;

using Avalonia.Controls;
using Avalonia.Interactivity;

/// <summary>Shows the selected entity path, secondary metrics, and contextual actions.</summary>
public partial class EntitySelectionFooter : UserControl
{
    public EntitySelectionFooter()
    {
        InitializeComponent();
    }

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        EntityActionsButton.Flyout?.Hide();
    }
}
