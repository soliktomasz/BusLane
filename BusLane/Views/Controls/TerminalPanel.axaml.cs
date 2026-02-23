namespace BusLane.Views.Controls;

using System.ComponentModel;
using Avalonia.Controls;
using BusLane.ViewModels.Core;

/// <summary>
/// Displays terminal output and command input.
/// </summary>
public partial class TerminalPanel : UserControl
{
    private TerminalHostViewModel? _viewModel;

    public TerminalPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(_viewModel, DataContext))
        {
            return;
        }

        if (_viewModel != null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as TerminalHostViewModel;
        if (_viewModel != null) _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? _, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TerminalHostViewModel.OutputText))
        {
            return;
        }

        if (this.FindControl<ScrollViewer>("TerminalOutputScrollViewer") is { } viewer)
        {
            viewer.Offset = new Avalonia.Vector(viewer.Offset.X, viewer.Extent.Height);
        }
    }
}
