namespace BusLane.Views.Controls;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BusLane.Models;
using BusLane.ViewModels;

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
            var mainWindow = TopLevel.GetTopLevel(this);
            if (mainWindow?.DataContext is MainWindowViewModel vm)
            {
                _ = vm.LoadTopicSubscriptionsCommand.ExecuteAsync(topic);
            }

            // Prevent the event from bubbling to the section Expander
            e.Handled = true;
        }
    }

    private void OnEntityRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && !e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            e.Handled = true;
        }
    }
}
