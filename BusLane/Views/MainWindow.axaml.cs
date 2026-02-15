namespace BusLane.Views;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using BusLane.Services.Infrastructure;
using BusLane.ViewModels;
using CommunityToolkit.Mvvm.Input;

public partial class MainWindow : Window
{
    private readonly Dictionary<KeyboardShortcutAction, Func<MainWindowViewModel, bool>> _shortcutHandlers;

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
}
