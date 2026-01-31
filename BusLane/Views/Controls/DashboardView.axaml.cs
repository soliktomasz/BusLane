namespace BusLane.Views.Controls;

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
