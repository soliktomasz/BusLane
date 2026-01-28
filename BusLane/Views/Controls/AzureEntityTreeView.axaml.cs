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
            // Get the ViewModel from the main window
            var mainWindow = TopLevel.GetTopLevel(this);
            if (mainWindow?.DataContext is MainWindowViewModel vm)
            {
                // Fire and forget - load subscriptions when expander opens
                _ = vm.LoadTopicSubscriptionsCommand.ExecuteAsync(topic);
            }
        }
    }

    private void OnTopicHeaderTapped(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Grid grid && grid.DataContext is TopicInfo topic)
        {
            var mainWindow = TopLevel.GetTopLevel(this);
            if (mainWindow?.DataContext is MainWindowViewModel vm)
            {
                // Select the topic
                vm.SelectTopicCommand.Execute(topic);
                // Toggle expansion
                topic.IsExpanded = !topic.IsExpanded;
            }
        }
    }
}

