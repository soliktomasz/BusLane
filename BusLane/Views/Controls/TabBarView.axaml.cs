// BusLane/Views/Controls/TabBarView.axaml.cs
using Avalonia.Controls;
using Avalonia.Input;
using BusLane.ViewModels;

namespace BusLane.Views.Controls;

public partial class TabBarView : UserControl
{
    public TabBarView()
    {
        InitializeComponent();
    }
    
    private void TabBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Only handle left mouse button clicks
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
            
        if (sender is Border border && border.Tag is string tabId)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.SwitchToTab(tabId);
                e.Handled = true;
            }
        }
    }
}
