namespace BusLane.Views.Controls;

using System.Collections.Specialized;
using System.ComponentModel;
using BusLane.ViewModels;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

/// <summary>
/// Panel displaying activity logs with filtering and search capabilities.
/// </summary>
public partial class LogViewerPanel : UserControl
{
    private LogViewerViewModel? _viewModel;
    private bool _scrollQueued;

    public LogViewerPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => BindViewModel();
        AttachedToVisualTree += (_, _) => BindViewModel();
        DetachedFromVisualTree += (_, _) => UnbindViewModel();
    }

    private void BindViewModel()
    {
        UnbindViewModel();
        _viewModel = DataContext as LogViewerViewModel;
        if (_viewModel == null) return;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.FilteredLogs.CollectionChanged += OnLogsChanged;
        QueueAutoScroll();
    }

    private void UnbindViewModel()
    {
        if (_viewModel == null) return;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.FilteredLogs.CollectionChanged -= OnLogsChanged;
        _viewModel = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LogViewerViewModel.IsAutoScrollEnabled) or nameof(LogViewerViewModel.IsOpen))
            QueueAutoScroll();
    }

    private void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs e) => QueueAutoScroll();

    private void QueueAutoScroll()
    {
        if (_scrollQueued || _viewModel is not { IsOpen: true, IsAutoScrollEnabled: true } || _viewModel.FilteredLogs.Count == 0)
            return;

        // Coalesce updates so a filter rebuild scrolls once after the list has settled.
        _scrollQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _scrollQueued = false;
            if (_viewModel is { IsOpen: true, IsAutoScrollEnabled: true } && _viewModel.FilteredLogs.Count > 0)
                LogListBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault()?.ScrollToHome();
        }, DispatcherPriority.Background);
    }
}
