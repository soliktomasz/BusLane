namespace BusLane.Views.Controls;

using Avalonia.Controls;
using Avalonia.Input;
using BusLane.ViewModels;

public partial class LiveStreamView : UserControl
{
    public LiveStreamView()
    {
        InitializeComponent();
    }

    private void OnSourceDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is LiveStreamViewModel { SelectedStreamEntity: not null } vm &&
            vm.StartStreamForSelectedEntityCommand.CanExecute(null))
        {
            vm.StartStreamForSelectedEntityCommand.Execute(null);
        }
    }
}
