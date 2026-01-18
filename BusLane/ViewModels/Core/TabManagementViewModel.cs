using System.Collections.ObjectModel;
using BusLane.Models;
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
        Action<ConnectionTabViewModel?> activeTabChanged)
    {
        _operationsFactory = operationsFactory;
        _preferencesService = preferencesService;
        _connectionStorage = connectionStorage;
        _auth = auth;
        _activeTabChanged = activeTabChanged;
    }

    /// <summary>
    /// Opens a new tab for given saved connection.
    /// </summary>
    public async Task OpenTabForConnectionAsync(SavedConnection connection)
    {
        var tab = new ConnectionTabViewModel(
            Guid.NewGuid().ToString(),
            connection.Name,
            connection.Endpoint ?? "",
            _preferencesService);

        ConnectionTabs.Add(tab);
        ActiveTab = tab;
        OnPropertyChanged(nameof(HasActiveTabs));

        try
        {
            await tab.ConnectWithConnectionStringAsync(connection, _operationsFactory);
        }
        catch
        {
            _activeTabChanged?.Invoke(ActiveTab);
        }
    }

    /// <summary>
    /// Opens a new tab for given Azure namespace.
    /// </summary>
    public async Task OpenTabForNamespaceAsync(ServiceBusNamespace ns)
    {
        if (_auth.Credential == null) return;

        var tab = new ConnectionTabViewModel(
            Guid.NewGuid().ToString(),
            ns.Name,
            ns.Endpoint,
            _preferencesService);

        ConnectionTabs.Add(tab);
        ActiveTab = tab;
        OnPropertyChanged(nameof(HasActiveTabs));

        try
        {
            await tab.ConnectWithAzureCredentialAsync(ns, _auth.Credential, _operationsFactory);
        }
        catch
        {
            _activeTabChanged?.Invoke(ActiveTab);
        }
    }

    /// <summary>
    /// Closes specified tab.
    /// </summary>
    public async Task CloseTabAsync(string tabId)
    {
        var tab = ConnectionTabs.FirstOrDefault(t => t.TabId == tabId);
        if (tab == null) return;

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
        }
        catch
        {
        }
    }

    /// <summary>
    /// Restores tabs from saved session.
    /// </summary>
    public async Task RestoreTabSessionAsync()
    {
        if (!_preferencesService.RestoreTabsOnStartup)
            return;

        try
        {
            var states = System.Text.Json.JsonSerializer.Deserialize<List<TabSessionState>>(_preferencesService.OpenTabsJson);
            if (states == null || states.Count == 0)
                return;

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
        }
        catch
        {
        }
    }

    partial void OnActiveTabChanged(ConnectionTabViewModel? value)
    {
        _activeTabChanged?.Invoke(value);
        SaveTabSession();
    }
}
