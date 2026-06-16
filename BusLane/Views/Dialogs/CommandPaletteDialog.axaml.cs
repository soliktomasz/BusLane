namespace BusLane.Views.Dialogs;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using BusLane.ViewModels;
using BusLane.ViewModels.Core;

public partial class CommandPaletteDialog : UserControl
{
    private MainWindowViewModel? _viewModel;

    public CommandPaletteDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => SetViewModel(null);
        KeyDown += OnKeyDown;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        SetViewModel(DataContext as MainWindowViewModel);
    }

    private void SetViewModel(MainWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        if (_viewModel != null)
        {
            _viewModel.CommandPalette.PropertyChanged -= OnCommandPalettePropertyChanged;
        }

        _viewModel = viewModel;

        if (_viewModel != null)
        {
            _viewModel.CommandPalette.PropertyChanged += OnCommandPalettePropertyChanged;
            if (_viewModel.CommandPalette.IsOpen)
            {
                FocusSearchBox();
            }
        }
    }

    private void OnCommandPalettePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommandPaletteViewModel.IsOpen) &&
            _viewModel?.CommandPalette.IsOpen == true)
        {
            FocusSearchBox();
        }
    }

    private void FocusSearchBox()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var searchBox = this.FindControl<TextBox>("CommandPaletteSearchTextBox");
            searchBox?.Focus();
            searchBox?.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter ||
            _viewModel?.CommandPalette.IsOpen != true ||
            _viewModel.CommandPalette.SelectedItem == null)
        {
            return;
        }

        e.Handled = true;
        _viewModel.ExecuteCommandPaletteItemCommand.Execute(_viewModel.CommandPalette.SelectedItem);
    }
}
