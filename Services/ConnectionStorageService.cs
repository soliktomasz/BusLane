using System.Text.Json;
using BusLane.Models;

namespace BusLane.Services;

public class ConnectionStorageService : IConnectionStorageService
{
    private readonly string _storageFilePath;
    private List<SavedConnection> _connections = [];

    public ConnectionStorageService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BusLane"
        );
        Directory.CreateDirectory(appDataPath);
        _storageFilePath = Path.Combine(appDataPath, "connections.json");
    }

    public async Task<IEnumerable<SavedConnection>> GetConnectionsAsync()
    {
        await LoadConnectionsAsync();
        return _connections.AsReadOnly();
    }

    public async Task SaveConnectionAsync(SavedConnection connection)
    {
        await LoadConnectionsAsync();
        
        // Remove existing connection with same ID if exists
        _connections.RemoveAll(c => c.Id == connection.Id);
        _connections.Add(connection);
        
        await PersistConnectionsAsync();
    }

    public async Task DeleteConnectionAsync(string connectionId)
    {
        await LoadConnectionsAsync();
        _connections.RemoveAll(c => c.Id == connectionId);
        await PersistConnectionsAsync();
    }

    public async Task<SavedConnection?> GetConnectionAsync(string connectionId)
    {
        await LoadConnectionsAsync();
        return _connections.FirstOrDefault(c => c.Id == connectionId);
    }

    public async Task ClearAllConnectionsAsync()
    {
        _connections.Clear();
        await PersistConnectionsAsync();
    }

    private async Task LoadConnectionsAsync()
    {
        if (!File.Exists(_storageFilePath))
        {
            _connections = [];
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_storageFilePath);
            _connections = JsonSerializer.Deserialize<List<SavedConnection>>(json, GetJsonOptions()) ?? [];
        }
        catch
        {
            _connections = [];
        }
    }

    private async Task PersistConnectionsAsync()
    {
        var json = JsonSerializer.Serialize(_connections, GetJsonOptions());
        await File.WriteAllTextAsync(_storageFilePath, json);
    }

    private static JsonSerializerOptions GetJsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

