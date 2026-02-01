using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BusLane.Models;
using BusLane.ViewModels;

namespace BusLane.Views.Controls;

public partial class AzureEntityTreeView : UserControl
{
    public AzureEntityTreeView()
    {
        InitializeComponent();
    }

    public void OnTopicExpanderExpanding(object? sender, CancelRoutedEventArgs e)
    {
        if (sender is Expander expander && expander.DataContext is TopicInfo topic)
        {
            var mainWindow = TopLevel.GetTopLevel(this);
            if (mainWindow?.DataContext is MainWindowViewModel vm)
            {
                _ = vm.LoadTopicSubscriptionsCommand.ExecuteAsync(topic);
            }

            // Prevent the event from bubbling to the section Expander
            e.Handled = true;
        }
    }

    private void OnTopicHeaderTapped(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Grid grid && grid.DataContext is TopicInfo topic)
        {
            var mainWindow = TopLevel.GetTopLevel(this);
            if (mainWindow?.DataContext is MainWindowViewModel vm)
            {
                vm.SelectTopicCommand.Execute(topic);
                // Don't toggle IsExpanded here - the Expander's built-in
                // ToggleButton handles it via the two-way binding
            }
        }
    }
}

