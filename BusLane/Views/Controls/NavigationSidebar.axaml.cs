using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BusLane.Views.Controls;

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
