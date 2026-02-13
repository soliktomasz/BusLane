using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Models.Logging;
using BusLane.Services.Abstractions;
using BusLane.Services.Auth;
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
    private readonly ILogSink _logSink;
    private readonly Action<string> _setStatus;
    private readonly Func<Task> _onConnected;
    private readonly Func<Task> _onDisconnected;
    private readonly Action<bool> _setNamespacePanelOpen;

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
        ILogSink logSink,
        Action<string> setStatus,
        Func<Task> onConnected,
        Func<Task> onDisconnected,
        Action<bool> setNamespacePanelOpen)
    {
        _auth = auth;
        _connectionStorage = connectionStorage;
        _logSink = logSink;
        _setStatus = setStatus;
        _onConnected = onConnected;
        _onDisconnected = onDisconnected;
        _setNamespacePanelOpen = setNamespacePanelOpen;

        _auth.AuthenticationChanged += (_, authenticated) => IsAuthenticated = authenticated;
        FavoriteConnections.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasFavoriteConnections));
    }

    public async Task InitializeAsync()
    {
        _setStatus("Loading...");
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Info,
            "Initializing connection manager..."));

        await LoadSavedConnectionsAsync();

        // Try to restore previous Azure session from cached credentials
        _setStatus("Checking for saved Azure session...");
        if (await _auth.TrySilentLoginAsync())
        {
            CurrentMode = ConnectionMode.AzureAccount;
            _setNamespacePanelOpen(true);
            _setStatus("Restored Azure session");
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.Application,
                LogLevel.Info,
                "Restored Azure session from cache"));
            await _onConnected();
            return;
        }

        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Debug,
            "No cached Azure session found"));

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
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Info,
            $"Loaded {SavedConnections.Count} saved connection(s)"));
    }

    [RelayCommand]
    public async Task LoginAsync()
    {
        _setStatus("Signing in to Azure...");
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Info,
            "Initiating Azure login..."));

        try
        {
            if (await _auth.LoginAsync())
            {
                CurrentMode = ConnectionMode.AzureAccount;
                ActiveConnection = null;
                _setNamespacePanelOpen(true);
                _setStatus("Ready");
                _logSink.Log(new LogEntry(
                    DateTime.UtcNow,
                    LogSource.Application,
                    LogLevel.Info,
                    "Azure login successful"));
                await _onConnected();
            }
            else
            {
                _setStatus("Sign in failed");
                _logSink.Log(new LogEntry(
                    DateTime.UtcNow,
                    LogSource.Application,
                    LogLevel.Warning,
                    "Azure login cancelled by user"));
            }
        }
        catch (Exception ex)
        {
            var errorMsg = "Azure login failed";
            _setStatus($"Error: {ex.Message}");
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.Application,
                LogLevel.Error,
                errorMsg,
                ex.Message));
        }
    }

    [RelayCommand]
    public async Task LogoutAsync()
    {
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Info,
            "Logging out from Azure..."));

        await _auth.LogoutAsync();
        CurrentMode = ConnectionMode.None;
        ActiveConnection = null;
        _setNamespacePanelOpen(false);
        await _onDisconnected();
        _setStatus("Disconnected");
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Info,
            "Logged out from Azure"));
    }

    [RelayCommand]
    public async Task DisconnectConnectionAsync()
    {
        var endpoint = ActiveConnection?.Endpoint ?? "Azure";
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Info,
            $"Disconnecting from {endpoint}..."));

        CurrentMode = ConnectionMode.None;
        ActiveConnection = null;
        await _onDisconnected();
        await LoadSavedConnectionsAsync();
        _setStatus("Disconnected");
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Info,
            $"Disconnected from {endpoint}"));
    }

    public void CloseConnectionLibrary()
    {
        ShowConnectionLibrary = false;
        ConnectionLibraryViewModel = null;
    }

    public async Task RefreshFavoriteConnectionsAsync()
    {
        FavoriteConnections.Clear();
        var connections = await _connectionStorage.GetConnectionsAsync();
        foreach (var conn in connections.Where(c => c.IsFavorite).OrderByDescending(c => c.CreatedAt))
            FavoriteConnections.Add(conn);
        OnPropertyChanged(nameof(HasFavoriteConnections));
    }
}
