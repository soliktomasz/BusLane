namespace BusLane.Views.Dialogs;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

public partial class AddWidgetDialog : UserControl
{
    public AddWidgetDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
