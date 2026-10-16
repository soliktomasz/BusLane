namespace BusLane.Views.Controls;

using Avalonia.Controls;
using Avalonia.Input;

/// <summary>
/// Aligned message row shared by the active and dead-letter lists.
/// </summary>
public partial class MessageRowView : UserControl
{
    public MessageRowView()
    {
        InitializeComponent();
    }

    private void OnMessageRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && !e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
            e.Handled = true;
    }
}
