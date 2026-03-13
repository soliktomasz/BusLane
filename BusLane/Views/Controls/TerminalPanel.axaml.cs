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
    private readonly ScrollViewer? _terminalOutputScrollViewer;

    public TerminalPanel()
    {
        InitializeComponent();
        _terminalOutputScrollViewer = this.FindControl<ScrollViewer>("TerminalOutputScrollViewer");
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

        if (_terminalOutputScrollViewer != null)
        {
            _terminalOutputScrollViewer.Offset = new Avalonia.Vector(
                _terminalOutputScrollViewer.Offset.X,
                _terminalOutputScrollViewer.Extent.Height);
        }
    }
}
