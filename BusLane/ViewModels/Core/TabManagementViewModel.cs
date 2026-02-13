using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Models.Logging;
using BusLane.Services.Abstractions;
using BusLane.Services.Auth;
using BusLane.Services.ServiceBus;
using BusLane.Services.Storage;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BusLane.ViewModels.Core;

/// <summary>
/// Manages connection tabs: create, close, switch, and persist session state.
/// </summary>
public partial class TabManagementViewModel : ViewModelBase
{
    private readonly IServiceBusOperationsFactory _operationsFactory;
    private readonly IPreferencesService _preferencesService;
    private readonly IConnectionStorageService _connectionStorage;
    private readonly IAzureAuthService _auth;
    private readonly ILogSink _logSink;
    private readonly Action<ConnectionTabViewModel?> _activeTabChanged;

    public ObservableCollection<ConnectionTabViewModel> ConnectionTabs { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveTabs))]
    private ConnectionTabViewModel? _activeTab;

    public bool HasActiveTabs => ConnectionTabs.Count > 0;

    public TabManagementViewModel(
        IServiceBusOperationsFactory operationsFactory,
        IPreferencesService preferencesService,
        IConnectionStorageService connectionStorage,
        IAzureAuthService auth,
        ILogSink logSink,
        Action<ConnectionTabViewModel?> activeTabChanged)
    {
        _operationsFactory = operationsFactory;
        _preferencesService = preferencesService;
        _connectionStorage = connectionStorage;
        _auth = auth;
        _logSink = logSink;
        _activeTabChanged = activeTabChanged;
    }

    /// <summary>
    /// Opens a new tab for given saved connection.
    /// </summary>
    public async Task OpenTabForConnectionAsync(SavedConnection connection)
    {
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Info,
            $"Opening tab for connection '{connection.Name}'"));

        var tab = new ConnectionTabViewModel(
            Guid.NewGuid().ToString(),
            connection.Name,
            connection.Endpoint ?? "",
            _preferencesService,
            _logSink);

        ConnectionTabs.Add(tab);
        ActiveTab = tab;
        OnPropertyChanged(nameof(HasActiveTabs));

        try
        {
            await tab.ConnectWithConnectionStringAsync(connection, _operationsFactory);
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.Application,
                LogLevel.Info,
                $"Opened tab '{tab.TabTitle}' (connection string mode)"));
        }
        catch (Exception ex)
        {
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.Application,
                LogLevel.Error,
                $"Failed to open tab for connection '{connection.Name}'",
                ex.Message));
            _activeTabChanged?.Invoke(ActiveTab);
        }
    }

    /// <summary>
    /// Opens a new tab for given Azure namespace.
    /// </summary>
    public async Task OpenTabForNamespaceAsync(ServiceBusNamespace ns)
    {
        if (_auth.Credential == null)
        {
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.Application,
                LogLevel.Warning,
                $"Cannot open tab for namespace '{ns.Name}' - Azure credential is not available"));
            return;
        }

        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Info,
            $"Opening tab for namespace '{ns.Name}'"));

        var tab = new ConnectionTabViewModel(
            Guid.NewGuid().ToString(),
            ns.Name,
            ns.Endpoint,
            _preferencesService,
            _logSink);

        ConnectionTabs.Add(tab);
        ActiveTab = tab;
        OnPropertyChanged(nameof(HasActiveTabs));

        try
        {
            await tab.ConnectWithAzureCredentialAsync(ns, _auth.Credential, _operationsFactory);
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.Application,
                LogLevel.Info,
                $"Opened tab '{tab.TabTitle}' (Azure mode)"));
        }
        catch (Exception ex)
        {
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.Application,
                LogLevel.Error,
                $"Failed to open tab for namespace '{ns.Name}'",
                ex.Message));
            _activeTabChanged?.Invoke(ActiveTab);
        }
    }

    /// <summary>
    /// Closes specified tab.
    /// </summary>
    public async Task CloseTabAsync(string tabId)
    {
        var tab = ConnectionTabs.FirstOrDefault(t => t.TabId == tabId);
        if (tab == null)
        {
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.Application,
                LogLevel.Debug,
                $"Close tab skipped - tab '{tabId}' was not found"));
            return;
        }

        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Info,
            $"Closing tab '{tab.TabTitle}'"));

        await tab.DisconnectAsync();

        var index = ConnectionTabs.IndexOf(tab);
        ConnectionTabs.Remove(tab);
        OnPropertyChanged(nameof(HasActiveTabs));

        if (ConnectionTabs.Count == 0)
        {
            ActiveTab = null;
        }
        else if (ActiveTab == tab)
        {
            var newIndex = Math.Min(index, ConnectionTabs.Count - 1);
            ActiveTab = ConnectionTabs[newIndex];
        }

        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Info,
            $"Closed tab '{tab.TabTitle}'. Remaining tab count: {ConnectionTabs.Count}"));
    }

    /// <summary>
    /// Switches to specified tab.
    /// </summary>
    public void SwitchToTab(string tabId)
    {
        var tab = ConnectionTabs.FirstOrDefault(t => t.TabId == tabId);
        if (tab != null)
        {
            ActiveTab = tab;
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.Application,
                LogLevel.Debug,
                $"Switched to tab '{tab.TabTitle}'"));
        }
    }

    /// <summary>
    /// Closes currently active tab.
    /// </summary>
    public async Task CloseActiveTabAsync()
    {
        if (ActiveTab != null)
        {
            await CloseTabAsync(ActiveTab.TabId);
        }
    }

    /// <summary>
    /// Switches to next tab in list.
    /// </summary>
    public void NextTab()
    {
        if (ConnectionTabs.Count <= 1) return;

        var currentIndex = ActiveTab != null ? ConnectionTabs.IndexOf(ActiveTab) : -1;
        var nextIndex = (currentIndex + 1) % ConnectionTabs.Count;
        ActiveTab = ConnectionTabs[nextIndex];
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Debug,
            $"Switched to next tab '{ActiveTab?.TabTitle}'"));
    }

    /// <summary>
    /// Switches to previous tab in list.
    /// </summary>
    public void PreviousTab()
    {
        if (ConnectionTabs.Count <= 1) return;

        var currentIndex = ActiveTab != null ? ConnectionTabs.IndexOf(ActiveTab) : 0;
        var prevIndex = currentIndex <= 0 ? ConnectionTabs.Count - 1 : currentIndex - 1;
        ActiveTab = ConnectionTabs[prevIndex];
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Debug,
            $"Switched to previous tab '{ActiveTab?.TabTitle}'"));
    }

    /// <summary>
    /// Switches to a tab by its 1-based index.
    /// </summary>
    public void SwitchToTabByIndex(int index)
    {
        var zeroBasedIndex = index - 1;
        if (zeroBasedIndex >= 0 && zeroBasedIndex < ConnectionTabs.Count)
        {
            ActiveTab = ConnectionTabs[zeroBasedIndex];
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.Application,
                LogLevel.Debug,
                $"Switched to tab {index}: '{ActiveTab?.TabTitle}'"));
        }
    }

    /// <summary>
    /// Saves current tab session to preferences.
    /// </summary>
    public void SaveTabSession()
    {
        try
        {
            var states = ConnectionTabs.Select((tab, index) => new TabSessionState
            {
                TabId = tab.TabId,
                Mode = tab.Mode,
                ConnectionId = tab.SavedConnection?.Id,
                NamespaceId = tab.Namespace?.Id,
                SelectedEntityName = tab.Navigation.CurrentEntityName,
                TabOrder = index
            }).ToList();

            _preferencesService.OpenTabsJson = System.Text.Json.JsonSerializer.Serialize(states);
            _preferencesService.Save();
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.Application,
                LogLevel.Debug,
                $"Saved tab session with {states.Count} tab(s)"));
        }
        catch (Exception ex)
        {
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.Application,
                LogLevel.Warning,
                "Failed to save tab session",
                ex.Message));
        }
    }

    /// <summary>
    /// Restores tabs from saved session.
    /// </summary>
    public async Task RestoreTabSessionAsync()
    {
        if (!_preferencesService.RestoreTabsOnStartup)
        {
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.Application,
                LogLevel.Debug,
                "Restore tab session skipped - disabled in preferences"));
            return;
        }

        try
        {
            var states = System.Text.Json.JsonSerializer.Deserialize<List<TabSessionState>>(_preferencesService.OpenTabsJson);
            if (states == null || states.Count == 0)
            {
                _logSink.Log(new LogEntry(
                    DateTime.UtcNow,
                    LogSource.Application,
                    LogLevel.Debug,
                    "No saved tab session found"));
                return;
            }

            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.Application,
                LogLevel.Info,
                $"Restoring {states.Count} tab(s) from previous session"));

            foreach (var state in states.OrderBy(s => s.TabOrder))
            {
                if (state.Mode == ConnectionMode.ConnectionString && state.ConnectionId != null)
                {
                    var connection = await _connectionStorage.GetConnectionAsync(state.ConnectionId);
                    if (connection != null)
                    {
                        await OpenTabForConnectionAsync(connection);
                    }
                }
            }

            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.Application,
                LogLevel.Info,
                "Tab session restore completed"));
        }
        catch (Exception ex)
        {
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.Application,
                LogLevel.Warning,
                "Failed to restore tab session",
                ex.Message));
        }
    }

    partial void OnActiveTabChanged(ConnectionTabViewModel? value)
    {
        _activeTabChanged?.Invoke(value);
        SaveTabSession();
    }
}
