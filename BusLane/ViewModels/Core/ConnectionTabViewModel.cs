// BusLane/ViewModels/Core/ConnectionTabViewModel.cs
using BusLane.Models;
using BusLane.Services.Abstractions;
using BusLane.Services.ServiceBus;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BusLane.ViewModels.Core;

/// <summary>
/// Represents a single connection tab with its own navigation state and message operations.
/// Each tab encapsulates a complete connection to a Service Bus namespace.
/// </summary>
public partial class ConnectionTabViewModel : ViewModelBase
{
    private readonly IPreferencesService _preferencesService;

    // Identity
    [ObservableProperty] private string _tabId;
    [ObservableProperty] private string _tabTitle;
    [ObservableProperty] private string _tabSubtitle;
    [ObservableProperty] private ConnectionMode _mode = ConnectionMode.None;

    // State
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string? _statusMessage;

    // Composed components
    public NavigationState Navigation { get; }
    public MessageOperationsViewModel MessageOps { get; }

    // Connection resources (set after connection)
    private IServiceBusOperations? _operations;
    private SavedConnection? _savedConnection;
    private ServiceBusNamespace? _namespace;

    public ConnectionTabViewModel(string tabId, string tabTitle, string tabSubtitle)
        : this(tabId, tabTitle, tabSubtitle, null!)
    {
    }

    public ConnectionTabViewModel(
        string tabId,
        string tabTitle,
        string tabSubtitle,
        IPreferencesService preferencesService)
    {
        _tabId = tabId;
        _tabTitle = tabTitle;
        _tabSubtitle = tabSubtitle;
        _preferencesService = preferencesService;

        Navigation = new NavigationState();

        MessageOps = new MessageOperationsViewModel(
            () => _operations,
            preferencesService ?? new DummyPreferencesService(),
            () => Navigation.CurrentEntityName,
            () => Navigation.CurrentSubscriptionName,
            () => Navigation.CurrentEntityRequiresSession,
            () => Navigation.ShowDeadLetter,
            msg => StatusMessage = msg);
    }

    /// <summary>
    /// Gets the current operations instance for this tab.
    /// </summary>
    public IServiceBusOperations? Operations => _operations;

    /// <summary>
    /// Gets the saved connection if connected via connection string.
    /// </summary>
    public SavedConnection? SavedConnection => _savedConnection;

    /// <summary>
    /// Gets the namespace if connected via Azure credentials.
    /// </summary>
    public ServiceBusNamespace? Namespace => _namespace;

    // Minimal implementation for parameterless constructor
    private class DummyPreferencesService : IPreferencesService
    {
        public bool ConfirmBeforeDelete { get; set; } = true;
        public bool ConfirmBeforePurge { get; set; } = true;
        public bool AutoRefreshMessages { get; set; }
        public int AutoRefreshIntervalSeconds { get; set; } = 30;
        public int DefaultMessageCount { get; set; } = 100;
        public bool ShowDeadLetterBadges { get; set; } = true;
        public bool EnableMessagePreview { get; set; } = true;
        public bool ShowNavigationPanel { get; set; } = true;
        public string Theme { get; set; } = "System";
        public event EventHandler? PreferencesChanged;
        public void Save() { }
        public void Load() { }
    }
}
