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

        if (sender is Border border && border.DataContext is ViewModels.Core.ConnectionTabViewModel tabVm)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.SwitchToTab(tabVm.TabId);
                // Note: Do NOT set e.Handled = true here as it interferes with cursor state updates
            }
        }
    }
}
