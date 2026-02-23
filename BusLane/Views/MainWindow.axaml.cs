namespace BusLane.Views;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using BusLane.Services.Infrastructure;
using BusLane.ViewModels;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.Input;

public partial class MainWindow : Window
{
    private const int TerminalBoundsSaveDebounceMilliseconds = 250;

    private readonly Dictionary<KeyboardShortcutAction, Func<MainWindowViewModel, bool>> _shortcutHandlers;
    private MainWindowViewModel? _viewModel;
    private TerminalWindow? _terminalWindow;
    private CancellationTokenSource? _saveTerminalBoundsCts;
    private bool _isClosing;
    private bool _isProgrammaticTerminalWindowClose;

    public MainWindow()
    {
        InitializeComponent();

        _shortcutHandlers = BuildShortcutHandlers();

        // Try to restore previous session when window loads
        Loaded += async (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                await vm.InitializeAsync();
            }
        };

        // Handle keyboard shortcuts
        KeyDown += OnKeyDown;
        DataContextChanged += OnDataContextChanged;
        Closing += OnMainWindowClosing;
        Closed += OnMainWindowClosed;
    }

    private Dictionary<KeyboardShortcutAction, Func<MainWindowViewModel, bool>> BuildShortcutHandlers()
    {
        return new Dictionary<KeyboardShortcutAction, Func<MainWindowViewModel, bool>>
        {
            // Navigation
            [KeyboardShortcutAction.Refresh] = vm =>
                Execute(vm.RefreshCommand),

            [KeyboardShortcutAction.ToggleNavigationPanel] = vm =>
                Execute(vm.ToggleNavigationPanelCommand),

            [KeyboardShortcutAction.FocusSearch] = vm =>
            {
                if (IsTextInputFocused())
                    return false;
                var searchBox = FindDescendantByName<TextBox>("MessageSearchTextBox");
                if (searchBox == null)
                    return false;
                searchBox.Focus();
                searchBox.SelectAll();
                return true;
            },

            // Messages
            [KeyboardShortcutAction.NewMessage] = vm =>
                vm.Navigation.CurrentEntityName != null && Execute(vm.OpenSendMessagePopupCommand),

            [KeyboardShortcutAction.CopyMessageBody] = vm =>
                !IsTextInputFocused() && vm.MessageOps.SelectedMessage != null && Execute(vm.CopyMessageBodyCommand),

            [KeyboardShortcutAction.DeleteSelected] = vm =>
                vm.MessageOps.HasSelectedMessages && Execute(vm.BulkDeleteMessagesAsyncCommand),

            [KeyboardShortcutAction.SelectAll] = vm =>
            {
                if (IsTextInputFocused() || vm.MessageOps.FilteredMessages.Count == 0)
                    return false;
                if (!vm.MessageOps.IsMultiSelectMode)
                    vm.ToggleMultiSelectModeCommand.Execute(null);
                vm.SelectAllMessagesCommand.Execute(null);
                return true;
            },

            [KeyboardShortcutAction.DeselectAll] = vm =>
                vm.MessageOps.IsMultiSelectMode && Execute(vm.DeselectAllMessagesCommand),

            [KeyboardShortcutAction.ToggleMultiSelect] = vm =>
                Execute(vm.ToggleMultiSelectModeCommand),

            [KeyboardShortcutAction.ToggleDeadLetter] = vm =>
                vm.Navigation.CurrentEntityName != null && Execute(vm.ToggleDeadLetterViewCommand),

            // Feature panels
            [KeyboardShortcutAction.OpenLiveStream] = vm =>
                Execute(vm.OpenLiveStreamCommand),

            [KeyboardShortcutAction.OpenCharts] = vm =>
                Execute(vm.OpenChartsCommand),

            [KeyboardShortcutAction.OpenAlerts] = vm =>
                Execute(vm.OpenAlertsCommand),

            [KeyboardShortcutAction.ToggleTerminal] = vm =>
                Execute(vm.ToggleTerminalCommand),

            [KeyboardShortcutAction.OpenSettings] = vm =>
                Execute(vm.OpenSettingsCommand),

            // Connections
            [KeyboardShortcutAction.OpenConnectionLibrary] = vm =>
                Execute(vm.OpenConnectionLibraryCommand),

            [KeyboardShortcutAction.Disconnect] = vm =>
            {
                if (vm.Connection.CurrentMode == ConnectionMode.None)
                    return false;
                if (vm.Connection.CurrentMode == ConnectionMode.AzureAccount)
                    vm.LogoutCommand.Execute(null);
                else
                    vm.DisconnectConnectionCommand.Execute(null);
                return true;
            },

            // Tabs
            [KeyboardShortcutAction.CloseTab] = vm =>
                vm.ActiveTab != null && Execute(vm.CloseActiveTabCommand),

            [KeyboardShortcutAction.NextTab] = vm =>
                vm.ConnectionTabs.Count > 1 && Execute(vm.NextTabCommand),

            [KeyboardShortcutAction.PreviousTab] = vm =>
                vm.ConnectionTabs.Count > 1 && Execute(vm.PreviousTabCommand),

            [KeyboardShortcutAction.SwitchToTab1] = vm =>
                Execute(vm.SwitchToTabByIndexCommand, 1),

            [KeyboardShortcutAction.SwitchToTab2] = vm =>
                Execute(vm.SwitchToTabByIndexCommand, 2),

            [KeyboardShortcutAction.SwitchToTab3] = vm =>
                Execute(vm.SwitchToTabByIndexCommand, 3),

            [KeyboardShortcutAction.SwitchToTab4] = vm =>
                Execute(vm.SwitchToTabByIndexCommand, 4),

            [KeyboardShortcutAction.SwitchToTab5] = vm =>
                Execute(vm.SwitchToTabByIndexCommand, 5),

            [KeyboardShortcutAction.SwitchToTab6] = vm =>
                Execute(vm.SwitchToTabByIndexCommand, 6),

            [KeyboardShortcutAction.SwitchToTab7] = vm =>
                Execute(vm.SwitchToTabByIndexCommand, 7),

            [KeyboardShortcutAction.SwitchToTab8] = vm =>
                Execute(vm.SwitchToTabByIndexCommand, 8),

            [KeyboardShortcutAction.SwitchToTab9] = vm =>
                Execute(vm.SwitchToTabByIndexCommand, 9),

            // Help
            [KeyboardShortcutAction.ShowHelp] = vm =>
                Execute(vm.ShowKeyboardShortcutsHelpCommand),
        };
    }

    private static bool Execute(IRelayCommand command, object? parameter = null)
    {
        command.Execute(parameter);
        return true;
    }

    /// <summary>
    /// Finds a control by name in the visual tree.
    /// </summary>
    private T? FindDescendantByName<T>(string name) where T : Control
    {
        return this.GetVisualDescendants()
            .OfType<T>()
            .FirstOrDefault(c => c.Name == name);
    }

    /// <summary>
    /// Checks if the currently focused element is a text input control.
    /// Used to avoid intercepting standard text editing shortcuts (Cmd+C, Cmd+A, etc.)
    /// </summary>
    private bool IsTextInputFocused()
    {
        var focusedElement = FocusManager?.GetFocusedElement();
        return focusedElement is TextBox or AutoCompleteBox;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var shortcuts = vm.KeyboardShortcuts;

        // Handle Escape key specially - close dialogs in priority order
        if (shortcuts.Matches(e, KeyboardShortcutAction.CloseDialog))
        {
            e.Handled = HandleEscape(vm);
            return;
        }

        // Don't process other shortcuts if a dialog is open
        if (vm.ShowKeyboardShortcuts || vm.ShowSettings || vm.ShowSendMessagePopup || vm.Confirmation.ShowConfirmDialog)
            return;

        // Dispatch via action map
        foreach (var (action, handler) in _shortcutHandlers)
        {
            if (shortcuts.Matches(e, action))
            {
                e.Handled = handler(vm);
                return;
            }
        }
    }

    private static bool HandleEscape(MainWindowViewModel vm)
    {
        if (vm.ShowKeyboardShortcuts)
        {
            vm.CloseKeyboardShortcutsCommand.Execute(null);
            return true;
        }
        if (vm.ShowSettings)
        {
            vm.CloseSettingsCommand.Execute(null);
            return true;
        }
        if (vm.ShowSendMessagePopup)
        {
            vm.CancelSendMessageCommand.Execute(null);
            return true;
        }
        if (vm.Confirmation.ShowConfirmDialog)
        {
            vm.Confirmation.CancelConfirmDialogCommand.Execute(null);
            return true;
        }
        if (vm.ShowStatusPopup)
        {
            vm.CloseStatusPopupCommand.Execute(null);
            return true;
        }
        if (vm.MessageOps.SelectedMessage != null)
        {
            vm.ClearSelectedMessageCommand.Execute(null);
            return true;
        }
        return false;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        SetViewModel(DataContext as MainWindowViewModel);
        ApplyTerminalLayoutRows();
        EnsureTerminalWindowState();
    }

    private void OnTerminalPropertyChanged(object? _, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TerminalHostViewModel.ShowTerminalPanel)
            or nameof(TerminalHostViewModel.TerminalIsDocked)
            or nameof(TerminalHostViewModel.TerminalDockHeight))
        {
            ApplyTerminalLayoutRows();
            EnsureTerminalWindowState();
        }
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        _isClosing = true;
        UpdateDockedTerminalHeight();
        CloseTerminalWindow();
    }

    private void OnMainWindowClosed(object? sender, EventArgs e)
    {
        SetViewModel(null);
        CancelPendingTerminalBoundsSave();
    }

    private void SetViewModel(MainWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        if (_viewModel != null)
        {
            _viewModel.Terminal.PropertyChanged -= OnTerminalPropertyChanged;
        }

        _viewModel = viewModel;

        if (_viewModel != null)
        {
            _viewModel.Terminal.PropertyChanged += OnTerminalPropertyChanged;
        }

        ApplyTerminalLayoutRows();
    }

    private void EnsureTerminalWindowState()
    {
        if (_viewModel == null)
        {
            return;
        }

        var terminal = _viewModel.Terminal;
        if (terminal.IsUndockedVisible)
        {
            EnsureTerminalWindowOpen(terminal);
            return;
        }

        CloseTerminalWindow();
    }

    private void EnsureTerminalWindowOpen(TerminalHostViewModel terminal)
    {
        if (_terminalWindow is { } existingWindow)
        {
            if (!existingWindow.IsVisible)
            {
                existingWindow.Show();
            }
            existingWindow.Activate();
            return;
        }

        var window = new TerminalWindow
        {
            DataContext = terminal
        };
        window.Closing += OnTerminalWindowClosing;
        window.PositionChanged += OnTerminalWindowPositionChanged;
        window.SizeChanged += OnTerminalWindowSizeChanged;

        var bounds = terminal.GetWindowBounds();
        if (bounds != null)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Position = new PixelPoint(bounds.X, bounds.Y);
            window.Width = bounds.Width;
            window.Height = bounds.Height;
        }

        _terminalWindow = window;
        window.Show(this);
        window.Activate();
    }

    private void CloseTerminalWindow()
    {
        if (_terminalWindow is not { } window)
        {
            return;
        }

        CancelPendingTerminalBoundsSave();
        SaveTerminalWindowBounds();

        window.Closing -= OnTerminalWindowClosing;
        window.PositionChanged -= OnTerminalWindowPositionChanged;
        window.SizeChanged -= OnTerminalWindowSizeChanged;
        _terminalWindow = null;

        _isProgrammaticTerminalWindowClose = true;
        try
        {
            window.Close();
        }
        finally
        {
            _isProgrammaticTerminalWindowClose = false;
        }
    }

    private void OnTerminalWindowClosing(object? _, WindowClosingEventArgs e)
    {
        if (_isClosing || _isProgrammaticTerminalWindowClose || _viewModel == null)
        {
            return;
        }

        var terminal = _viewModel.Terminal;
        if (!terminal.IsUndockedVisible)
        {
            return;
        }

        e.Cancel = true;
        SaveTerminalWindowBounds();
        terminal.DockCommand.Execute(null);
    }

    private void OnTerminalWindowPositionChanged(object? sender, PixelPointEventArgs e) => DebounceSaveTerminalWindowBounds();

    private void OnTerminalWindowSizeChanged(object? sender, SizeChangedEventArgs e) => DebounceSaveTerminalWindowBounds();

    private void OnTerminalGridSplitterPointerReleased(object? sender, PointerReleasedEventArgs e) => UpdateDockedTerminalHeight();

    private void SaveTerminalWindowBounds()
    {
        if (_viewModel?.Terminal is not { } terminal || _terminalWindow is not { } window)
        {
            return;
        }

        var size = window.ClientSize;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var position = window.Position;
        terminal.UpdateWindowBounds(position.X, position.Y, size.Width, size.Height);
    }

    private void DebounceSaveTerminalWindowBounds()
    {
        CancelPendingTerminalBoundsSave();

        var cts = new CancellationTokenSource();
        _saveTerminalBoundsCts = cts;
        _ = SaveTerminalWindowBoundsDebouncedAsync(cts.Token);
    }

    private async Task SaveTerminalWindowBoundsDebouncedAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TerminalBoundsSaveDebounceMilliseconds, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (ct.IsCancellationRequested)
        {
            return;
        }

        SaveTerminalWindowBounds();
    }

    private void CancelPendingTerminalBoundsSave()
    {
        _saveTerminalBoundsCts?.Cancel();
        _saveTerminalBoundsCts?.Dispose();
        _saveTerminalBoundsCts = null;
    }

    private void UpdateDockedTerminalHeight()
    {
        if (_viewModel?.Terminal.IsDockedVisible != true)
        {
            return;
        }

        if (this.FindControl<Grid>("MainContentGrid") is not { } grid || grid.RowDefinitions.Count <= 3)
        {
            return;
        }

        var terminalRow = grid.RowDefinitions[3];
        var height = terminalRow.ActualHeight > 0 ? terminalRow.ActualHeight : terminalRow.Height.Value;
        if (height <= 0)
        {
            return;
        }

        _viewModel.Terminal.TerminalDockHeight = height;
    }

    private void ApplyTerminalLayoutRows()
    {
        if (_viewModel?.Terminal is not { } terminal)
        {
            return;
        }

        if (this.FindControl<Grid>("MainContentGrid") is not { } grid || grid.RowDefinitions.Count <= 3)
        {
            return;
        }

        grid.RowDefinitions[3].Height = terminal.IsDockedVisible
            ? new GridLength(Math.Max(0, terminal.TerminalDockHeight))
            : new GridLength(0);
    }
}
