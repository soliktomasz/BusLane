using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Models.Logging;
using BusLane.ViewModels.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusLane.ViewModels;

using Services.ServiceBus;
using Services.Storage;

public partial class ConnectionLibraryViewModel : ViewModelBase
{
    private readonly IConnectionStorageService _connectionStorage;
    private readonly IServiceBusOperationsFactory _operationsFactory;
    private readonly ILogSink _logSink;
    private readonly Action<SavedConnection> _onConnectionSelected;
    private readonly Action<string> _onStatusUpdate;
    private readonly Func<Task>? _onFavoritesChanged;

    [ObservableProperty] private string _newConnectionName = "";
    [ObservableProperty] private string _newConnectionString = "";
    [ObservableProperty] private bool _isValidating;
    [ObservableProperty] private string? _validationMessage;
    [ObservableProperty] private bool _isAddingConnection;
    [ObservableProperty] private bool _isEditingConnection;
    [ObservableProperty] private SavedConnection? _selectedConnection;
    [ObservableProperty] private SavedConnection? _editingConnection;
    [ObservableProperty] private string? _detectedInfo;
    [ObservableProperty] private ConnectionEnvironment _newConnectionEnvironment = ConnectionEnvironment.None;
    [ObservableProperty] private ConnectionEnvironment _selectedEnvironmentTab = ConnectionEnvironment.None;
    [ObservableProperty] private bool _isCheckingConnection;
    [ObservableProperty] private string? _checkConnectionResult;
    [ObservableProperty] private bool _checkConnectionSuccess;


    public ObservableCollection<SavedConnection> SavedConnections { get; } = [];
    public ObservableCollection<SavedConnection> FilteredConnections { get; } = [];
    public IReadOnlyList<ConnectionEnvironment> AvailableEnvironments { get; } =
    [
        ConnectionEnvironment.None,
        ConnectionEnvironment.Development,
        ConnectionEnvironment.Test,
        ConnectionEnvironment.Production
    ];

    public ConnectionLibraryViewModel(
        IConnectionStorageService connectionStorage,
        IServiceBusOperationsFactory operationsFactory,
        ILogSink logSink,
        Action<SavedConnection> onConnectionSelected,
        Action<string> onStatusUpdate,
        Func<Task>? onFavoritesChanged = null)
    {
        _connectionStorage = connectionStorage;
        _operationsFactory = operationsFactory;
        _logSink = logSink;
        _onConnectionSelected = onConnectionSelected;
        _onStatusUpdate = onStatusUpdate;
        _onFavoritesChanged = onFavoritesChanged;
    }

    public async Task LoadConnectionsAsync()
    {
        SavedConnections.Clear();
        var connections = await _connectionStorage.GetConnectionsAsync();
        foreach (var conn in connections.OrderByDescending(c => c.CreatedAt))
        {
            SavedConnections.Add(conn);
        }
        UpdateFilteredConnections();
    }

    partial void OnSelectedEnvironmentTabChanged(ConnectionEnvironment value)
    {
        UpdateFilteredConnections();
    }

    private void UpdateFilteredConnections()
    {
        FilteredConnections.Clear();
        var filtered = SelectedEnvironmentTab == ConnectionEnvironment.None
            ? SavedConnections
            : SavedConnections.Where(c => c.Environment == SelectedEnvironmentTab);

        foreach (var conn in filtered)
        {
            FilteredConnections.Add(conn);
        }
    }

    [RelayCommand]
    private void StartAddConnection()
    {
        IsAddingConnection = true;
        IsEditingConnection = false;
        EditingConnection = null;
        NewConnectionName = "";
        NewConnectionString = "";
        NewConnectionEnvironment = ConnectionEnvironment.None;
        ValidationMessage = null;
        DetectedInfo = null;
        CheckConnectionResult = null;
    }

    [RelayCommand]
    private void StartEditConnection(SavedConnection connection)
    {
        IsAddingConnection = true;
        IsEditingConnection = true;
        EditingConnection = connection;
        NewConnectionName = connection.Name;
        NewConnectionString = connection.ConnectionString;
        NewConnectionEnvironment = connection.Environment;
        ValidationMessage = null;
        DetectedInfo = null;
        CheckConnectionResult = null;
    }

    [RelayCommand]
    private void CancelAddConnection()
    {
        IsAddingConnection = false;
        IsEditingConnection = false;
        EditingConnection = null;
        ValidationMessage = null;
        DetectedInfo = null;
        CheckConnectionResult = null;
        NewConnectionEnvironment = ConnectionEnvironment.None;
    }

    [RelayCommand]
    private async Task ValidateAndAddConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(NewConnectionName))
        {
            ValidationMessage = "Please enter a connection name";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewConnectionString))
        {
            ValidationMessage = "Please enter a connection string";
            return;
        }

        IsValidating = true;
        ValidationMessage = "Validating connection...";
        DetectedInfo = null;

        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Info,
            $"Validating connection '{NewConnectionName}'..."));

        try
        {
            var ops = _operationsFactory.CreateFromConnectionString(NewConnectionString);
            var (isValid, _, endpoint, errorMessage) = await ops.ValidateAsync();

            if (!isValid)
            {
                ValidationMessage = $"Invalid connection: {errorMessage}";
                _logSink.Log(new LogEntry(
                    DateTime.UtcNow,
                    LogSource.Application,
                    LogLevel.Error,
                    $"Connection validation failed for '{NewConnectionName}'",
                    errorMessage));
                IsValidating = false;
                return;
            }

            // For namespace connections, try to detect entities
            var info = await ops.GetNamespaceInfoAsync();
            if (info != null)
            {
                DetectedInfo = $"Found {info.QueueCount} queue(s), {info.TopicCount} topic(s)";
            }

            SavedConnection connection;
            if (IsEditingConnection && EditingConnection != null)
            {
                // Update existing connection
                connection = new SavedConnection
                {
                    Id = EditingConnection.Id,
                    Name = NewConnectionName,
                    ConnectionString = NewConnectionString,
                    Type = ConnectionType.Namespace,
                    EntityName = null,
                    CreatedAt = EditingConnection.CreatedAt,
                    IsFavorite = EditingConnection.IsFavorite,
                    Environment = NewConnectionEnvironment
                };

                await _connectionStorage.SaveConnectionAsync(connection);
                var index = SavedConnections.IndexOf(EditingConnection);
                if (index >= 0)
                {
                    SavedConnections[index] = connection;
                }
                UpdateFilteredConnections();
                _onStatusUpdate($"Connection '{NewConnectionName}' updated successfully");
                _logSink.Log(new LogEntry(
                    DateTime.UtcNow,
                    LogSource.Application,
                    LogLevel.Info,
                    $"Updated connection '{NewConnectionName}' ({endpoint})"));
            }
            else
            {
                // Create new connection
                connection = new SavedConnection
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = NewConnectionName,
                    ConnectionString = NewConnectionString,
                    Type = ConnectionType.Namespace,
                    EntityName = null,
                    CreatedAt = DateTimeOffset.UtcNow,
                    Environment = NewConnectionEnvironment
                };

                await _connectionStorage.SaveConnectionAsync(connection);
                SavedConnections.Insert(0, connection);
                UpdateFilteredConnections();
                _onStatusUpdate($"Connection '{NewConnectionName}' saved successfully");
                _logSink.Log(new LogEntry(
                    DateTime.UtcNow,
                    LogSource.Application,
                    LogLevel.Info,
                    $"Added new connection '{NewConnectionName}' ({endpoint})"));
            }

            IsAddingConnection = false;
            IsEditingConnection = false;
            EditingConnection = null;
            ValidationMessage = null;
            DetectedInfo = null;
            CheckConnectionResult = null;
            NewConnectionEnvironment = ConnectionEnvironment.None;

            // Automatically connect to the newly added/edited connection
            _onConnectionSelected(connection);
        }
        catch (Exception ex)
        {
            ValidationMessage = $"Error: {ex.Message}";
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.Application,
                LogLevel.Error,
                $"Failed to save connection '{NewConnectionName}'",
                ex.Message));
        }
        finally
        {
            IsValidating = false;
        }
    }

    [RelayCommand]
    private async Task CheckConnectionAsync()
    {
        if (string.IsNullOrWhiteSpace(NewConnectionString))
        {
            CheckConnectionResult = "Please enter a connection string first";
            CheckConnectionSuccess = false;
            return;
        }

        IsCheckingConnection = true;
        CheckConnectionResult = null;
        DetectedInfo = null;

        try
        {
            var ops = _operationsFactory.CreateFromConnectionString(NewConnectionString);
            var (isValid, _, endpoint, errorMessage) = await ops.ValidateAsync();

            if (!isValid)
            {
                CheckConnectionResult = $"Connection failed: {errorMessage}";
                CheckConnectionSuccess = false;
                _logSink.Log(new LogEntry(
                    DateTime.UtcNow,
                    LogSource.Application,
                    LogLevel.Error,
                    "Connection check failed",
                    errorMessage));
                IsCheckingConnection = false;
                return;
            }

            // Try to get namespace info
            var info = await ops.GetNamespaceInfoAsync();
            if (info != null)
            {
                CheckConnectionResult = $"Connection successful! Found {info.QueueCount} queue(s), {info.TopicCount} topic(s)";
                DetectedInfo = $"Endpoint: {endpoint}";
            }
            else
            {
                CheckConnectionResult = "Connection successful!";
            }
            CheckConnectionSuccess = true;
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.Application,
                LogLevel.Info,
                $"Connection check successful: {endpoint}"));
        }
        catch (Exception ex)
        {
            CheckConnectionResult = $"Connection failed: {ex.Message}";
            CheckConnectionSuccess = false;
            _logSink.Log(new LogEntry(
                DateTime.UtcNow,
                LogSource.Application,
                LogLevel.Error,
                "Connection check failed",
                ex.Message));
        }
        finally
        {
            IsCheckingConnection = false;
        }
    }

    [RelayCommand]
    private async Task DeleteConnectionAsync(SavedConnection connection)
    {
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Warning,
            $"Deleting connection '{connection.Name}'"));

        await _connectionStorage.DeleteConnectionAsync(connection.Id);
        SavedConnections.Remove(connection);
        UpdateFilteredConnections();
        _onStatusUpdate($"Connection '{connection.Name}' deleted");
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Info,
            $"Deleted connection '{connection.Name}'"));
    }

    [RelayCommand]
    private async Task ClearAllConnectionsAsync()
    {
        var count = SavedConnections.Count;
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Warning,
            $"Clearing all {count} saved connection(s)"));

        await _connectionStorage.ClearAllConnectionsAsync();
        SavedConnections.Clear();
        UpdateFilteredConnections();
        _onStatusUpdate("All connections cleared");
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Info,
            $"Cleared {count} connection(s)"));
    }

    [RelayCommand]
    private void SelectConnection(SavedConnection connection)
    {
        SelectedConnection = connection;
        _onConnectionSelected(connection);
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(SavedConnection connection)
    {
        var updatedConnection = connection with { IsFavorite = !connection.IsFavorite };
        await _connectionStorage.SaveConnectionAsync(updatedConnection);

        var index = SavedConnections.IndexOf(connection);
        if (index >= 0)
        {
            SavedConnections[index] = updatedConnection;
        }
        UpdateFilteredConnections();

        if (_onFavoritesChanged != null)
        {
            await _onFavoritesChanged();
        }

        var isFavorite = updatedConnection.IsFavorite;
        _onStatusUpdate(isFavorite
            ? $"'{connection.Name}' added to favorites"
            : $"'{connection.Name}' removed from favorites");
        _logSink.Log(new LogEntry(
            DateTime.UtcNow,
            LogSource.Application,
            LogLevel.Info,
            isFavorite
                ? $"Added '{connection.Name}' to favorites"
                : $"Removed '{connection.Name}' from favorites"));
    }
}
