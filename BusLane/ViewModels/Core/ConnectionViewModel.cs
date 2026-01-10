using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Services.Abstractions;
using BusLane.Services.Auth;
using BusLane.Services.ServiceBus;
using BusLane.Services.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusLane.ViewModels.Core;

/// <summary>
/// Handles connection management: Azure auth, saved connections, connection lifecycle.
/// </summary>
public partial class ConnectionViewModel : ViewModelBase
{
    private readonly IAzureAuthService _auth;
    private readonly IConnectionStorageService _connectionStorage;
    private readonly IServiceBusOperationsFactory _operationsFactory;
    private readonly Action<string> _setStatus;
    private readonly Func<Task> _onConnected;
    private readonly Func<Task> _onDisconnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAzureSections))]
    private bool _isAuthenticated;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAzureSections))]
    private ConnectionMode _currentMode = ConnectionMode.None;

    [ObservableProperty] private SavedConnection? _activeConnection;
    [ObservableProperty] private bool _showConnectionLibrary;
    [ObservableProperty] private ConnectionLibraryViewModel? _connectionLibraryViewModel;

    public ObservableCollection<SavedConnection> SavedConnections { get; } = [];
    public ObservableCollection<SavedConnection> FavoriteConnections { get; } = [];

    public bool ShowAzureSections => IsAuthenticated && CurrentMode == ConnectionMode.AzureAccount;
    public bool HasFavoriteConnections => FavoriteConnections.Count > 0;

    /// <summary>
    /// Gets the current connection string (null if in Azure account mode).
    /// </summary>
    public string? CurrentConnectionString => 
        CurrentMode == ConnectionMode.ConnectionString ? ActiveConnection?.ConnectionString : null;

    /// <summary>
    /// Gets the current endpoint (from namespace or saved connection).
    /// </summary>
    public string? CurrentEndpoint => ActiveConnection?.Endpoint;

    public ConnectionViewModel(
        IAzureAuthService auth,
        IConnectionStorageService connectionStorage,
        IServiceBusOperationsFactory operationsFactory,
        Action<string> setStatus,
        Func<Task> onConnected,
        Func<Task> onDisconnected)
    {
        _auth = auth;
        _connectionStorage = connectionStorage;
        _operationsFactory = operationsFactory;
        _setStatus = setStatus;
        _onConnected = onConnected;
        _onDisconnected = onDisconnected;

        _auth.AuthenticationChanged += (_, authenticated) => IsAuthenticated = authenticated;
        FavoriteConnections.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasFavoriteConnections));
    }

    public async Task InitializeAsync()
    {
        _setStatus("Loading...");
        await LoadSavedConnectionsAsync();

        // Try to restore previous Azure session from cached credentials
        _setStatus("Checking for saved Azure session...");
        if (await _auth.TrySilentLoginAsync())
        {
            CurrentMode = ConnectionMode.AzureAccount;
            _setStatus("Restored Azure session");
            await _onConnected();
            return;
        }

        _setStatus(SavedConnections.Count > 0
            ? "Select a saved connection or sign in with Azure"
            : "Add a connection or sign in with Azure to get started");
    }

    public async Task LoadSavedConnectionsAsync()
    {
        SavedConnections.Clear();
        FavoriteConnections.Clear();
        var connections = await _connectionStorage.GetConnectionsAsync();
        foreach (var conn in connections.OrderByDescending(c => c.CreatedAt))
        {
            SavedConnections.Add(conn);
            if (conn.IsFavorite)
                FavoriteConnections.Add(conn);
        }
        OnPropertyChanged(nameof(HasFavoriteConnections));
    }

    [RelayCommand]
    public async Task LoginAsync()
    {
        _setStatus("Signing in to Azure...");

        try
        {
            if (await _auth.LoginAsync())
            {
                CurrentMode = ConnectionMode.AzureAccount;
                ActiveConnection = null;
                _setStatus("Ready");
                await _onConnected();
            }
            else
            {
                _setStatus("Sign in failed");
            }
        }
        catch (Exception ex)
        {
            _setStatus($"Error: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task LogoutAsync()
    {
        await _auth.LogoutAsync();
        CurrentMode = ConnectionMode.None;
        ActiveConnection = null;
        await _onDisconnected();
        _setStatus("Disconnected");
    }

    [RelayCommand]
    public async Task ConnectToSavedConnectionAsync(SavedConnection connection)
    {
        _setStatus($"Connecting to {connection.Name}...");

        try
        {
            CurrentMode = ConnectionMode.ConnectionString;
            ActiveConnection = connection;
            await _onConnected();
            _setStatus($"Connected to {connection.Name}");
        }
        catch (Exception ex)
        {
            _setStatus($"Error: {ex.Message}");
            CurrentMode = ConnectionMode.None;
            ActiveConnection = null;
        }
    }

    [RelayCommand]
    public async Task DisconnectConnectionAsync()
    {
        CurrentMode = ConnectionMode.None;
        ActiveConnection = null;
        await _onDisconnected();
        await LoadSavedConnectionsAsync();
        _setStatus("Disconnected");
    }

    [RelayCommand]
    public async Task OpenConnectionLibraryAsync()
    {
        ConnectionLibraryViewModel = new ConnectionLibraryViewModel(
            _connectionStorage,
            _operationsFactory,
            async conn =>
            {
                ShowConnectionLibrary = false;
                ConnectionLibraryViewModel = null;
                await ConnectToSavedConnectionAsync(conn);
            },
            _setStatus,
            RefreshFavoriteConnectionsAsync
        );
        await ConnectionLibraryViewModel.LoadConnectionsAsync();
        ShowConnectionLibrary = true;
    }

    public void CloseConnectionLibrary()
    {
        ShowConnectionLibrary = false;
        ConnectionLibraryViewModel = null;
    }

    private async Task RefreshFavoriteConnectionsAsync()
    {
        FavoriteConnections.Clear();
        var connections = await _connectionStorage.GetConnectionsAsync();
        foreach (var conn in connections.Where(c => c.IsFavorite).OrderByDescending(c => c.CreatedAt))
            FavoriteConnections.Add(conn);
        OnPropertyChanged(nameof(HasFavoriteConnections));
    }
}
