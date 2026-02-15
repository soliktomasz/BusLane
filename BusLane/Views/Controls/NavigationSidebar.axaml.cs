namespace BusLane.Views.Controls;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

public partial class NavigationSidebar : UserControl
{
    public NavigationSidebar()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
