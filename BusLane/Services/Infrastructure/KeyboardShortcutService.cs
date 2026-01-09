using System.Runtime.InteropServices;
using Avalonia.Input;

namespace BusLane.Services.Infrastructure;

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
    OpenSettings,
    
    // Connections
    OpenConnectionLibrary,
    Disconnect,
    
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
                primary | KeyModifiers.Shift, Key.D),
            
            [KeyboardShortcutAction.ToggleMultiSelect] = new(
                KeyboardShortcutAction.ToggleMultiSelect, "Messages", "Toggle multi-select mode",
                primary, Key.M),
            
            [KeyboardShortcutAction.ToggleDeadLetter] = new(
                KeyboardShortcutAction.ToggleDeadLetter, "Messages", "Toggle dead letter view",
                primary | KeyModifiers.Shift, Key.L),
            
            // Feature panel shortcuts
            [KeyboardShortcutAction.OpenLiveStream] = new(
                KeyboardShortcutAction.OpenLiveStream, "Features", "Open live stream",
                primary, Key.D),
            
            [KeyboardShortcutAction.OpenCharts] = new(
                KeyboardShortcutAction.OpenCharts, "Features", "Open charts & metrics",
                primary | KeyModifiers.Shift, Key.C),
            
            [KeyboardShortcutAction.OpenAlerts] = new(
                KeyboardShortcutAction.OpenAlerts, "Features", "Open alerts",
                primary | KeyModifiers.Shift, Key.A),
            
            [KeyboardShortcutAction.OpenSettings] = new(
                KeyboardShortcutAction.OpenSettings, "Features", "Open settings",
                primary, Key.OemComma),
            
            // Connection shortcuts
            [KeyboardShortcutAction.OpenConnectionLibrary] = new(
                KeyboardShortcutAction.OpenConnectionLibrary, "Connections", "Open connection library",
                primary, Key.K),
            
            [KeyboardShortcutAction.Disconnect] = new(
                KeyboardShortcutAction.Disconnect, "Connections", "Disconnect",
                primary | KeyModifiers.Shift, Key.D),
            
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

