using System.Collections.ObjectModel;
using BusLane.Models;
using BusLane.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BusLane.ViewModels;

public partial class ConnectionLibraryViewModel : ViewModelBase
{
    private readonly IConnectionStorageService _connectionStorage;
    private readonly IConnectionStringService _connectionStringService;
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


    public ObservableCollection<SavedConnection> SavedConnections { get; } = [];

    public ConnectionLibraryViewModel(
        IConnectionStorageService connectionStorage,
        IConnectionStringService connectionStringService,
        Action<SavedConnection> onConnectionSelected,
        Action<string> onStatusUpdate,
        Func<Task>? onFavoritesChanged = null)
    {
        _connectionStorage = connectionStorage;
        _connectionStringService = connectionStringService;
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
    }

    [RelayCommand]
    private void StartAddConnection()
    {
        IsAddingConnection = true;
        IsEditingConnection = false;
        EditingConnection = null;
        NewConnectionName = "";
        NewConnectionString = "";
        ValidationMessage = null;
        DetectedInfo = null;
    }

    [RelayCommand]
    private void StartEditConnection(SavedConnection connection)
    {
        IsAddingConnection = true;
        IsEditingConnection = true;
        EditingConnection = connection;
        NewConnectionName = connection.Name;
        NewConnectionString = connection.ConnectionString;
        ValidationMessage = null;
        DetectedInfo = null;
    }

    [RelayCommand]
    private void CancelAddConnection()
    {
        IsAddingConnection = false;
        IsEditingConnection = false;
        EditingConnection = null;
        ValidationMessage = null;
        DetectedInfo = null;
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

        try
        {
            var (isValid, _, _, errorMessage) = 
                await _connectionStringService.ValidateConnectionStringAsync(NewConnectionString);

            if (!isValid)
            {
                ValidationMessage = $"Invalid connection: {errorMessage}";
                return;
            }

            // For namespace connections, try to detect entities
            var info = await _connectionStringService.GetNamespaceInfoAsync(NewConnectionString);
            if (info != null)
            {
                DetectedInfo = $"Found {info.QueueCount} queue(s), {info.TopicCount} topic(s)";
            }

            SavedConnection connection;
            if (IsEditingConnection && EditingConnection != null)
            {
                // Update existing connection
                connection = new SavedConnection(
                    Id: EditingConnection.Id,
                    Name: NewConnectionName,
                    ConnectionString: NewConnectionString,
                    Type: ConnectionType.Namespace,
                    EntityName: null,
                    CreatedAt: EditingConnection.CreatedAt
                );
                
                await _connectionStorage.SaveConnectionAsync(connection);
                var index = SavedConnections.IndexOf(EditingConnection);
                if (index >= 0)
                {
                    SavedConnections[index] = connection;
                }
                _onStatusUpdate($"Connection '{NewConnectionName}' updated successfully");
            }
            else
            {
                // Create new connection
                connection = new SavedConnection(
                    Id: Guid.NewGuid().ToString(),
                    Name: NewConnectionName,
                    ConnectionString: NewConnectionString,
                    Type: ConnectionType.Namespace,
                    EntityName: null,
                    CreatedAt: DateTimeOffset.UtcNow
                );

                await _connectionStorage.SaveConnectionAsync(connection);
                SavedConnections.Insert(0, connection);
                _onStatusUpdate($"Connection '{NewConnectionName}' saved successfully");
            }

            IsAddingConnection = false;
            IsEditingConnection = false;
            EditingConnection = null;
            ValidationMessage = null;
            DetectedInfo = null;
            
            // Automatically connect to the newly added/edited connection
            _onConnectionSelected(connection);
        }
        catch (Exception ex)
        {
            ValidationMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsValidating = false;
        }
    }

    [RelayCommand]
    private async Task DeleteConnectionAsync(SavedConnection connection)
    {
        await _connectionStorage.DeleteConnectionAsync(connection.Id);
        SavedConnections.Remove(connection);
        _onStatusUpdate($"Connection '{connection.Name}' deleted");
    }

    [RelayCommand]
    private async Task ClearAllConnectionsAsync()
    {
        await _connectionStorage.ClearAllConnectionsAsync();
        SavedConnections.Clear();
        _onStatusUpdate("All connections cleared");
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
        
        if (_onFavoritesChanged != null)
        {
            await _onFavoritesChanged();
        }
        
        _onStatusUpdate(updatedConnection.IsFavorite 
            ? $"'{connection.Name}' added to favorites" 
            : $"'{connection.Name}' removed from favorites");
    }
}

