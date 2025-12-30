namespace BusLane.Services.Storage;

using System.Text.Json;
using BusLane.Models;
using Infrastructure;

public class ConnectionStorageService : IConnectionStorageService
{
    private readonly string _storageFilePath;
    private readonly IEncryptionService _encryptionService;
    private List<SavedConnection> _connections = [];

    public ConnectionStorageService(IEncryptionService encryptionService)
    {
        _encryptionService = encryptionService;

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
            var storedConnections = JsonSerializer.Deserialize<List<StoredConnection>>(json, GetJsonOptions()) ?? [];

            _connections = storedConnections
                .Select(stored =>
                {
                    // Decrypt the connection string
                    // The decryption handles both encrypted and legacy unencrypted strings
                    var connectionString = _encryptionService.Decrypt(stored.EncryptedConnectionString)
                                          ?? stored.EncryptedConnectionString;

                    return new SavedConnection(
                        stored.Id,
                        stored.Name,
                        connectionString,
                        stored.Type,
                        stored.EntityName,
                        stored.CreatedAt,
                        stored.IsFavorite,
                        stored.Environment
                    );
                })
                .ToList();
        }
        catch
        {
            // Try loading legacy format (unencrypted SavedConnection)
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
    }

    private async Task PersistConnectionsAsync()
    {
        // Convert to stored format with encrypted connection strings
        var storedConnections = _connections
            .Select(conn => new StoredConnection(
                conn.Id,
                conn.Name,
                _encryptionService.Encrypt(conn.ConnectionString),
                conn.Type,
                conn.EntityName,
                conn.CreatedAt,
                conn.IsFavorite,
                conn.Environment
            ))
            .ToList();

        var json = JsonSerializer.Serialize(storedConnections, GetJsonOptions());
        await File.WriteAllTextAsync(_storageFilePath, json);
    }

    private static JsonSerializerOptions GetJsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

