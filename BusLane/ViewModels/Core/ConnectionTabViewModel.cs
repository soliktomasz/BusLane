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

    // Connection resources (set after connection)
    private IServiceBusOperations? _operations;
    private SavedConnection? _savedConnection;
    private ServiceBusNamespace? _namespace;

    public ConnectionTabViewModel(string tabId, string tabTitle, string tabSubtitle)
    {
        _tabId = tabId;
        _tabTitle = tabTitle;
        _tabSubtitle = tabSubtitle;
        Navigation = new NavigationState();
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
}
