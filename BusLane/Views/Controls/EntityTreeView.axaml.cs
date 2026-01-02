using Avalonia.Controls;
using Avalonia.Interactivity;
using BusLane.Models;
using BusLane.ViewModels;

namespace BusLane.Views.Controls;

public partial class EntityTreeView : UserControl
{
    public EntityTreeView()
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
}

