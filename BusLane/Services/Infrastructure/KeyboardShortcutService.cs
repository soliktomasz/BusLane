namespace BusLane.Services.Infrastructure;

using System.Runtime.InteropServices;
using Avalonia.Input;

/// <summary>
/// Service for managing keyboard shortcuts in the application.
/// Provides platform-aware modifier keys (Cmd on macOS, Ctrl on Windows/Linux).
/// </summary>
public interface IKeyboardShortcutService
{
    /// <summary>Gets the primary modifier key for the current platform (Cmd on macOS, Ctrl on others).</summary>
    KeyModifiers PrimaryModifier { get; }
    
    /// <summary>Gets the display name for the primary modifier key.</summary>
    string PrimaryModifierDisplay { get; }
    
    /// <summary>Gets all available keyboard shortcuts with their descriptions.</summary>
    IReadOnlyList<KeyboardShortcut> GetAllShortcuts();
    
    /// <summary>Checks if the given key event matches the specified shortcut.</summary>
    bool Matches(KeyEventArgs e, KeyboardShortcutAction action);
    
    /// <summary>Gets the key gesture for a specific action.</summary>
    KeyGesture? GetGesture(KeyboardShortcutAction action);
}

public enum KeyboardShortcutAction
{
    // Navigation
    Refresh,
    ToggleNavigationPanel,
    FocusSearch,
    
    // Messages
    NewMessage,
    CopyMessageBody,
    DeleteSelected,
    SelectAll,
    DeselectAll,
    ToggleMultiSelect,
    ToggleDeadLetter,
    
    // Feature Panels
    OpenLiveStream,
    OpenCharts,
    OpenAlerts,
    ToggleTerminal,
    OpenSettings,
    
    // Connections
    OpenConnectionLibrary,
    Disconnect,

    // Tabs
    CloseTab,
    NextTab,
    PreviousTab,
    SwitchToTab1,
    SwitchToTab2,
    SwitchToTab3,
    SwitchToTab4,
    SwitchToTab5,
    SwitchToTab6,
    SwitchToTab7,
    SwitchToTab8,
    SwitchToTab9,

    // General
    CloseDialog,
    ShowHelp
}

public record KeyboardShortcut(
    KeyboardShortcutAction Action,
    string Category,
    string Description,
    KeyModifiers Modifiers,
    Key Key)
{
    public string DisplayText => FormatShortcut(Modifiers, Key);
    
    private static string FormatShortcut(KeyModifiers modifiers, Key key)
    {
        var parts = new List<string>();
        
        var isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        
        if (modifiers.HasFlag(KeyModifiers.Meta))
            parts.Add(isMac ? "⌘" : "Win");
        if (modifiers.HasFlag(KeyModifiers.Control))
            parts.Add(isMac ? "⌃" : "Ctrl");
        if (modifiers.HasFlag(KeyModifiers.Alt))
            parts.Add(isMac ? "⌥" : "Alt");
        if (modifiers.HasFlag(KeyModifiers.Shift))
            parts.Add(isMac ? "⇧" : "Shift");
        
        parts.Add(FormatKey(key));
        
        return string.Join(isMac ? "" : "+", parts);
    }
    
    private static string FormatKey(Key key) => key switch
    {
        Key.Escape => "Esc",
        Key.Delete => "Del",
        Key.Back => "⌫",
        Key.Return => "↵",
        Key.OemComma => ",",
        Key.OemQuestion => "?",
        _ => key.ToString()
    };
}

public class KeyboardShortcutService : IKeyboardShortcutService
{
    private static readonly bool IsMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    
    private readonly Dictionary<KeyboardShortcutAction, KeyboardShortcut> _shortcuts;
    
    public KeyModifiers PrimaryModifier => IsMac ? KeyModifiers.Meta : KeyModifiers.Control;
    public string PrimaryModifierDisplay => IsMac ? "⌘" : "Ctrl";
    
    public KeyboardShortcutService()
    {
        var primary = PrimaryModifier;
        
        _shortcuts = new Dictionary<KeyboardShortcutAction, KeyboardShortcut>
        {
            // Navigation shortcuts
            [KeyboardShortcutAction.Refresh] = new(
                KeyboardShortcutAction.Refresh, "Navigation", "Refresh current view",
                primary, Key.R),
            
            [KeyboardShortcutAction.ToggleNavigationPanel] = new(
                KeyboardShortcutAction.ToggleNavigationPanel, "Navigation", "Toggle navigation panel",
                primary, Key.B),
            
            [KeyboardShortcutAction.FocusSearch] = new(
                KeyboardShortcutAction.FocusSearch, "Navigation", "Focus message search",
                primary, Key.F),
            
            // Message shortcuts
            [KeyboardShortcutAction.NewMessage] = new(
                KeyboardShortcutAction.NewMessage, "Messages", "Send new message",
                primary, Key.N),
            
            [KeyboardShortcutAction.CopyMessageBody] = new(
                KeyboardShortcutAction.CopyMessageBody, "Messages", "Copy message body",
                primary, Key.C),
            
            [KeyboardShortcutAction.DeleteSelected] = new(
                KeyboardShortcutAction.DeleteSelected, "Messages", "Delete selected messages",
                primary, Key.Delete),
            
            [KeyboardShortcutAction.SelectAll] = new(
                KeyboardShortcutAction.SelectAll, "Messages", "Select all messages",
                primary, Key.A),
            
            [KeyboardShortcutAction.DeselectAll] = new(
                KeyboardShortcutAction.DeselectAll, "Messages", "Deselect all messages",
                primary | KeyModifiers.Shift, Key.U),
            
            [KeyboardShortcutAction.ToggleMultiSelect] = new(
                KeyboardShortcutAction.ToggleMultiSelect, "Messages", "Toggle multi-select mode",
                primary, Key.M),
            
            [KeyboardShortcutAction.ToggleDeadLetter] = new(
                KeyboardShortcutAction.ToggleDeadLetter, "Messages", "Toggle dead letter view",
                primary, Key.D),
            
            // Feature panel shortcuts
            [KeyboardShortcutAction.OpenLiveStream] = new(
                KeyboardShortcutAction.OpenLiveStream, "Features", "Open live stream",
                primary | KeyModifiers.Shift, Key.L),
            
            [KeyboardShortcutAction.OpenCharts] = new(
                KeyboardShortcutAction.OpenCharts, "Features", "Open charts & metrics",
                primary | KeyModifiers.Shift, Key.O),
            
            [KeyboardShortcutAction.OpenAlerts] = new(
                KeyboardShortcutAction.OpenAlerts, "Features", "Open alerts",
                primary | KeyModifiers.Shift, Key.E),

            [KeyboardShortcutAction.ToggleTerminal] = new(
                KeyboardShortcutAction.ToggleTerminal, "Features", "Toggle terminal",
                primary | KeyModifiers.Shift, Key.T),
            
            [KeyboardShortcutAction.OpenSettings] = new(
                KeyboardShortcutAction.OpenSettings, "Features", "Open settings",
                primary, Key.OemComma),
            
            // Connection shortcuts
            [KeyboardShortcutAction.OpenConnectionLibrary] = new(
                KeyboardShortcutAction.OpenConnectionLibrary, "Connections", "Open connection library",
                primary, Key.K),
            
            [KeyboardShortcutAction.Disconnect] = new(
                KeyboardShortcutAction.Disconnect, "Connections", "Disconnect",
                primary | KeyModifiers.Shift, Key.W),

            // Tab shortcuts
            [KeyboardShortcutAction.CloseTab] = new(
                KeyboardShortcutAction.CloseTab, "Tabs", "Close current tab",
                primary, Key.W),

            [KeyboardShortcutAction.NextTab] = new(
                KeyboardShortcutAction.NextTab, "Tabs", "Switch to next tab",
                primary | KeyModifiers.Shift, Key.OemCloseBrackets),

            [KeyboardShortcutAction.PreviousTab] = new(
                KeyboardShortcutAction.PreviousTab, "Tabs", "Switch to previous tab",
                primary | KeyModifiers.Shift, Key.OemOpenBrackets),

            [KeyboardShortcutAction.SwitchToTab1] = new(
                KeyboardShortcutAction.SwitchToTab1, "Tabs", "Switch to tab 1",
                primary, Key.D1),

            [KeyboardShortcutAction.SwitchToTab2] = new(
                KeyboardShortcutAction.SwitchToTab2, "Tabs", "Switch to tab 2",
                primary, Key.D2),

            [KeyboardShortcutAction.SwitchToTab3] = new(
                KeyboardShortcutAction.SwitchToTab3, "Tabs", "Switch to tab 3",
                primary, Key.D3),

            [KeyboardShortcutAction.SwitchToTab4] = new(
                KeyboardShortcutAction.SwitchToTab4, "Tabs", "Switch to tab 4",
                primary, Key.D4),

            [KeyboardShortcutAction.SwitchToTab5] = new(
                KeyboardShortcutAction.SwitchToTab5, "Tabs", "Switch to tab 5",
                primary, Key.D5),

            [KeyboardShortcutAction.SwitchToTab6] = new(
                KeyboardShortcutAction.SwitchToTab6, "Tabs", "Switch to tab 6",
                primary, Key.D6),

            [KeyboardShortcutAction.SwitchToTab7] = new(
                KeyboardShortcutAction.SwitchToTab7, "Tabs", "Switch to tab 7",
                primary, Key.D7),

            [KeyboardShortcutAction.SwitchToTab8] = new(
                KeyboardShortcutAction.SwitchToTab8, "Tabs", "Switch to tab 8",
                primary, Key.D8),

            [KeyboardShortcutAction.SwitchToTab9] = new(
                KeyboardShortcutAction.SwitchToTab9, "Tabs", "Switch to tab 9",
                primary, Key.D9),

            // General shortcuts
            [KeyboardShortcutAction.CloseDialog] = new(
                KeyboardShortcutAction.CloseDialog, "General", "Close dialog / Cancel",
                KeyModifiers.None, Key.Escape),
            
            [KeyboardShortcutAction.ShowHelp] = new(
                KeyboardShortcutAction.ShowHelp, "General", "Show keyboard shortcuts",
                primary, Key.OemQuestion),
        };
    }
    
    public IReadOnlyList<KeyboardShortcut> GetAllShortcuts() => 
        _shortcuts.Values.OrderBy(s => s.Category).ThenBy(s => s.Description).ToList();
    
    public bool Matches(KeyEventArgs e, KeyboardShortcutAction action)
    {
        if (!_shortcuts.TryGetValue(action, out var shortcut))
            return false;
        
        // Check if modifiers match
        var eventModifiers = e.KeyModifiers;
        if (eventModifiers != shortcut.Modifiers)
            return false;
        
        // Check if key matches
        return e.Key == shortcut.Key;
    }
    
    public KeyGesture? GetGesture(KeyboardShortcutAction action)
    {
        if (!_shortcuts.TryGetValue(action, out var shortcut))
            return null;
        
        return new KeyGesture(shortcut.Key, shortcut.Modifiers);
    }
}
