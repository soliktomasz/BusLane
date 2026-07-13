namespace BusLane.Services.Storage;

using System.Text.Json;
using BusLane.Models;
using BusLane.Services.Infrastructure;
using Serilog;
using static BusLane.Services.Infrastructure.SafeJsonSerializer;

public class ConnectionStorageService : IConnectionStorageService
{
    // Cached JsonSerializerOptions to avoid recreation on every serialization
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IEncryptionService _encryptionService;
    private readonly string _storagePath;
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private List<SavedConnection> _connections = [];
    private bool _loaded;

    public ConnectionStorageService(IEncryptionService encryptionService, string? storagePath = null)
    {
        _encryptionService = encryptionService;
        _storagePath = storagePath ?? AppPaths.Connections;

        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public async Task<IEnumerable<SavedConnection>> GetConnectionsAsync()
    {
        await _mutationGate.WaitAsync();
        try
        {
            await LoadConnectionsAsync();
            return _connections.ToList().AsReadOnly();
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public async Task SaveConnectionAsync(SavedConnection connection)
    {
        await _mutationGate.WaitAsync();
        try
        {
            await LoadConnectionsAsync();
            _connections.RemoveAll(c => c.Id == connection.Id);
            _connections.Add(connection);
            PersistConnections();
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public async Task DeleteConnectionAsync(string connectionId)
    {
        await _mutationGate.WaitAsync();
        try
        {
            await LoadConnectionsAsync();
            _connections.RemoveAll(c => c.Id == connectionId);
            PersistConnections();
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public async Task<SavedConnection?> GetConnectionAsync(string connectionId)
    {
        await _mutationGate.WaitAsync();
        try
        {
            await LoadConnectionsAsync();
            return _connections.FirstOrDefault(c => c.Id == connectionId);
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    public async Task ClearAllConnectionsAsync()
    {
        await _mutationGate.WaitAsync();
        try
        {
            _connections.Clear();
            _loaded = true;
            PersistConnections();
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    private async Task LoadConnectionsAsync()
    {
        if (_loaded)
            return;

        List<SavedConnection>? loadedConnections = null;

        if (!File.Exists(_storagePath))
        {
            _connections = [];
            _loaded = true;
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_storagePath);
            try
            {
                var storedConnections = DeserializeList<StoredConnection>(json, JsonOptions);

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
                            Log.Warning("Failed to decrypt a connection. The connection may have been encrypted with a different key");
                            Log.Debug("Failed to decrypt connection {ConnectionName} (ID: {ConnectionId})", stored.Name, stored.Id);
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
                    loadedConnections = DeserializeList<SavedConnection>(json, JsonOptions);
                    Log.Information("Loaded {Count} connections from legacy format", loadedConnections.Count);
                }
                catch (Exception legacyEx)
                {
                    Log.Warning(legacyEx, "Failed to load connections from {Path}, starting with empty list", _storagePath);
                    loadedConnections = [];
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load connections from {Path}, starting with empty list", _storagePath);
            loadedConnections = [];
        }

        _connections = loadedConnections ?? [];
        _loaded = true;
    }

    private void PersistConnections()
    {
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

        var json = JsonSerializer.Serialize(storedConnections, JsonOptions);
        AppPaths.CreateSecureFile(_storagePath, json);
    }
}
