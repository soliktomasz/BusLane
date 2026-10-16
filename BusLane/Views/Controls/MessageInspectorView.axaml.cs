namespace BusLane.Views.Controls;

using Avalonia.Controls;

/// <summary>
/// Read-only message inspection alongside the message list.
/// </summary>
public partial class MessageInspectorView : UserControl
{
    public MessageInspectorView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Classes.Set("compact", Bounds.Height < 400);
    }
}
