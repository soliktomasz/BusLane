namespace BusLane.Views.Controls;

using Avalonia.Controls;
using Avalonia.Input;

public partial class MessagesPanelView : UserControl
{
    public MessagesPanelView()
    {
        InitializeComponent();
    }

    private void OnMessageRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && !e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            e.Handled = true;
        }
    }
}
