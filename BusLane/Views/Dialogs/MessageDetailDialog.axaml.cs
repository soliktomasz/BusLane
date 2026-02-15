namespace BusLane.Views.Dialogs;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

public partial class MessageDetailDialog : UserControl
{
    public MessageDetailDialog()
    {
        InitializeComponent();
        
        // Reset scroll position when the modal becomes visible
        ModalOverlay.PropertyChanged += OnModalOverlayPropertyChanged;
    }
    
    private void OnModalOverlayPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty && e.NewValue is true)
        {
            // Use dispatcher to ensure scroll happens after layout is complete
            Dispatcher.UIThread.Post(() => ContentScrollViewer?.ScrollToHome(), DispatcherPriority.Loaded);
        }
    }
}

