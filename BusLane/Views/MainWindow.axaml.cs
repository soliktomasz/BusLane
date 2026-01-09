using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using BusLane.Services.Infrastructure;
using BusLane.ViewModels;

namespace BusLane.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
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

    /// <summary>
    /// Finds a control by name in the visual tree.
    /// </summary>
    private T? FindDescendantByName<T>(string name) where T : Control
    {
        return this.GetVisualDescendants()
            .OfType<T>()
            .FirstOrDefault(c => c.Name == name);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        var shortcuts = vm.KeyboardShortcuts;
        
        // Handle Escape key specially - close any open dialogs
        if (shortcuts.Matches(e, KeyboardShortcutAction.CloseDialog))
        {
            if (vm.ShowKeyboardShortcuts)
            {
                vm.CloseKeyboardShortcutsCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (vm.ShowSettings)
            {
                vm.CloseSettingsCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (vm.ShowSendMessagePopup)
            {
                vm.CancelSendMessageCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (vm.ShowConfirmDialog)
            {
                vm.CancelConfirmDialogCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (vm.ShowStatusPopup)
            {
                vm.CloseStatusPopupCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (vm.MessageOps.SelectedMessage != null)
            {
                vm.ClearSelectedMessageCommand.Execute(null);
                e.Handled = true;
            }
        }
        
        // Don't process other shortcuts if a dialog is open
        if (vm.ShowKeyboardShortcuts || vm.ShowSettings || vm.ShowSendMessagePopup || vm.ShowConfirmDialog)
            return;
        
        // Navigation shortcuts
        if (shortcuts.Matches(e, KeyboardShortcutAction.Refresh))
        {
            vm.RefreshCommand.Execute(null);
            e.Handled = true;
        }
        else if (shortcuts.Matches(e, KeyboardShortcutAction.ToggleNavigationPanel))
        {
            vm.ToggleNavigationPanelCommand.Execute(null);
            e.Handled = true;
        }
        else if (shortcuts.Matches(e, KeyboardShortcutAction.FocusSearch))
        {
            // Find the MessageSearchTextBox in the visual tree and focus it
            var searchBox = FindDescendantByName<TextBox>("MessageSearchTextBox");
            if (searchBox != null)
            {
                searchBox.Focus();
                searchBox.SelectAll();
                e.Handled = true;
            }
        }
        // Message shortcuts
        else if (shortcuts.Matches(e, KeyboardShortcutAction.NewMessage))
        {
            if (vm.Navigation.CurrentEntityName != null)
            {
                vm.OpenSendMessagePopupCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if (shortcuts.Matches(e, KeyboardShortcutAction.CopyMessageBody))
        {
            if (vm.MessageOps.SelectedMessage != null)
            {
                vm.CopyMessageBodyCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if (shortcuts.Matches(e, KeyboardShortcutAction.DeleteSelected))
        {
            if (vm.MessageOps.HasSelectedMessages)
            {
                vm.BulkDeleteMessagesAsyncCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if (shortcuts.Matches(e, KeyboardShortcutAction.SelectAll))
        {
            if (vm.MessageOps.IsMultiSelectMode)
            {
                vm.SelectAllMessagesCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if (shortcuts.Matches(e, KeyboardShortcutAction.DeselectAll))
        {
            if (vm.MessageOps.IsMultiSelectMode)
            {
                vm.DeselectAllMessagesCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if (shortcuts.Matches(e, KeyboardShortcutAction.ToggleMultiSelect))
        {
            vm.ToggleMultiSelectModeCommand.Execute(null);
            e.Handled = true;
        }
        else if (shortcuts.Matches(e, KeyboardShortcutAction.ToggleDeadLetter))
        {
            if (vm.Navigation.CurrentEntityName != null)
            {
                vm.ToggleDeadLetterViewCommand.Execute(null);
                e.Handled = true;
            }
        }
        // Feature panel shortcuts
        else if (shortcuts.Matches(e, KeyboardShortcutAction.OpenLiveStream))
        {
            vm.OpenLiveStreamCommand.Execute(null);
            e.Handled = true;
        }
        else if (shortcuts.Matches(e, KeyboardShortcutAction.OpenCharts))
        {
            vm.OpenChartsCommand.Execute(null);
            e.Handled = true;
        }
        else if (shortcuts.Matches(e, KeyboardShortcutAction.OpenAlerts))
        {
            vm.OpenAlertsCommand.Execute(null);
            e.Handled = true;
        }
        else if (shortcuts.Matches(e, KeyboardShortcutAction.OpenSettings))
        {
            vm.OpenSettingsCommand.Execute(null);
            e.Handled = true;
        }
        // Connection shortcuts
        else if (shortcuts.Matches(e, KeyboardShortcutAction.OpenConnectionLibrary))
        {
            vm.OpenConnectionLibraryCommand.Execute(null);
            e.Handled = true;
        }
        else if (shortcuts.Matches(e, KeyboardShortcutAction.Disconnect))
        {
            if (vm.Connection.CurrentMode != ConnectionMode.None)
            {
                if (vm.Connection.CurrentMode == ConnectionMode.AzureAccount)
                    vm.LogoutCommand.Execute(null);
                else
                    vm.DisconnectConnectionCommand.Execute(null);
                e.Handled = true;
            }
        }
        // Help
        else if (shortcuts.Matches(e, KeyboardShortcutAction.ShowHelp))
        {
            vm.ShowKeyboardShortcutsHelpCommand.Execute(null);
            e.Handled = true;
        }
    }
}
