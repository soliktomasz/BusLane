namespace BusLane.Views.Dialogs;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using BusLane.ViewModels;

public partial class MessageDetailDialog : UserControl
{
    private IInputElement? _previousFocus;

    public MessageDetailDialog()
    {
        InitializeComponent();
        
        // Reset scroll position and focus when the drawer becomes visible.
        ModalOverlay.PropertyChanged += OnModalOverlayPropertyChanged;
    }
    
    private void OnModalOverlayPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty && e.NewValue is true)
        {
            _previousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            // Use dispatcher to ensure scroll happens after layout is complete
            Dispatcher.UIThread.Post(() =>
            {
                ContentScrollViewer.ScrollToHome();
                CloseButton.Focus();
            }, DispatcherPriority.Loaded);
        }
        else if (e.Property == IsVisibleProperty && e.NewValue is false)
        {
            var previousFocus = _previousFocus;
            _previousFocus = null;
            Dispatcher.UIThread.Post(() => previousFocus?.Focus(), DispatcherPriority.Loaded);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is MainWindowViewModel vm)
        {
            vm.ClearSelectedMessageCommand.Execute(null);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }
}
