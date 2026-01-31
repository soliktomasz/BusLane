namespace BusLane.Services.Storage;

using System.Text.Json;
using BusLane.Models;
using BusLane.Services.Infrastructure;
using Serilog;

public class ConnectionStorageService : IConnectionStorageService
{
    // Cached JsonSerializerOptions to avoid recreation on every serialization
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IEncryptionService _encryptionService;
    private readonly object _lock = new();
    private List<SavedConnection> _connections = [];
    private bool _loaded;

    public ConnectionStorageService(IEncryptionService encryptionService)
    {
        _encryptionService = encryptionService;
        AppPaths.EnsureDirectoryExists();
    }

    public async Task<IEnumerable<SavedConnection>> GetConnectionsAsync()
    {
        await LoadConnectionsAsync();
        lock (_lock)
        {
            return _connections.ToList().AsReadOnly();
        }
    }

    public async Task SaveConnectionAsync(SavedConnection connection)
    {
        await LoadConnectionsAsync();

        lock (_lock)
        {
            // Remove existing connection with same ID if exists
            _connections.RemoveAll(c => c.Id == connection.Id);
            _connections.Add(connection);
        }

        await PersistConnectionsAsync();
    }

    public async Task DeleteConnectionAsync(string connectionId)
    {
        await LoadConnectionsAsync();

        lock (_lock)
        {
            _connections.RemoveAll(c => c.Id == connectionId);
        }

        await PersistConnectionsAsync();
    }

    public async Task<SavedConnection?> GetConnectionAsync(string connectionId)
    {
        await LoadConnectionsAsync();

        lock (_lock)
        {
            return _connections.FirstOrDefault(c => c.Id == connectionId);
        }
    }

    public async Task ClearAllConnectionsAsync()
    {
        lock (_lock)
        {
            _connections.Clear();
            _loaded = true;
        }

        await PersistConnectionsAsync();
    }

    private async Task LoadConnectionsAsync()
    {
        if (_loaded)
            return;

        List<SavedConnection>? loadedConnections = null;

        if (!File.Exists(AppPaths.Connections))
        {
            lock (_lock)
            {
                _connections = [];
                _loaded = true;
            }
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(AppPaths.Connections);
            var storedConnections = JsonSerializer.Deserialize<List<StoredConnection>>(json, JsonOptions) ?? [];

            loadedConnections = storedConnections
                .Select(stored =>
                {
                    // Decrypt the connection string
                    // The decryption handles both encrypted and legacy unencrypted strings
                    var connectionString = _encryptionService.Decrypt(stored.EncryptedConnectionString);

                    // If decryption returns null, the encrypted data is corrupted or
                    // was encrypted with a different key (e.g., from a different machine)
                    if (connectionString == null)
                    {
                        Log.Warning("Failed to decrypt connection {ConnectionName} (ID: {ConnectionId}). " +
                                    "The connection may have been encrypted with a different key",
                            stored.Name, stored.Id);
                        return null;
                    }

                    return new SavedConnection
                    {
                        Id = stored.Id,
                        Name = stored.Name,
                        ConnectionString = connectionString,
                        Type = stored.Type,
                        EntityName = stored.EntityName,
                        CreatedAt = stored.CreatedAt,
                        IsFavorite = stored.IsFavorite,
                        Environment = stored.Environment
                    };
                })
                .Where(conn => conn != null)
                .Cast<SavedConnection>()
                .ToList();
        }
        catch (Exception ex)
        {
            // Try loading legacy format (unencrypted SavedConnection)
            Log.Debug(ex, "Failed to load connections in encrypted format, trying legacy format");
            try
            {
                var json = await File.ReadAllTextAsync(AppPaths.Connections);
                loadedConnections = JsonSerializer.Deserialize<List<SavedConnection>>(json, JsonOptions) ?? [];
                Log.Information("Loaded {Count} connections from legacy format", loadedConnections.Count);
            }
            catch (Exception legacyEx)
            {
                Log.Warning(legacyEx, "Failed to load connections from {Path}, starting with empty list", AppPaths.Connections);
                loadedConnections = [];
            }
        }

        lock (_lock)
        {
            _connections = loadedConnections ?? [];
            _loaded = true;
        }
    }

    private async Task PersistConnectionsAsync()
    {
        List<StoredConnection> storedConnections;

        lock (_lock)
        {
            // Convert to stored format with encrypted connection strings
            storedConnections = _connections
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
        }

        var json = JsonSerializer.Serialize(storedConnections, JsonOptions);
        await File.WriteAllTextAsync(AppPaths.Connections, json);
    }
}

