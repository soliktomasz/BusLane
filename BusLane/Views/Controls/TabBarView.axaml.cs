// BusLane/Views/Controls/TabBarView.axaml.cs
using Avalonia.Controls;
using Avalonia.Interactivity;
using BusLane.ViewModels;

namespace BusLane.Views.Controls;

public partial class TabBarView : UserControl
{
    public TabBarView()
    {
        InitializeComponent();
    }

    private void TabRadioButton_Checked(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radioButton &&
            radioButton.DataContext is ViewModels.Core.ConnectionTabViewModel tabVm &&
            radioButton.IsChecked == true &&
            DataContext is MainWindowViewModel vm)
        {
            vm.SwitchToTab(tabVm.TabId);
        }
    }
}
