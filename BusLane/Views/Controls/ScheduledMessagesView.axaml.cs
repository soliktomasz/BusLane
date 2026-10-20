namespace BusLane.Views.Controls;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

public partial class ScheduledMessagesView : UserControl
{
    public ScheduledMessagesView()
    {
        InitializeComponent();
    }

    // Row menus live in per-row flyouts; close the owning popup once an item is chosen.
    private void OnMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control && control.FindLogicalAncestorOfType<Popup>() is { } popup)
            popup.IsOpen = false;
    }
}
