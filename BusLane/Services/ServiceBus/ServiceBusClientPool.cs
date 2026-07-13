namespace BusLane.Services.ServiceBus;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Serilog;

/// <summary>
/// A lightweight connection pool for Service Bus clients that enables sharing
/// connections across multiple tabs/operations with the same connection string.
/// </summary>
public sealed class ServiceBusClientPool : IDisposable, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, PooledClient> _clients = new();
    private readonly object _lifecycleLock = new();
    private bool _disposed;

    /// <summary>
    /// Gets or creates a ServiceBusClient for the given connection string.
    /// Multiple calls with the same connection string return the same instance.
    /// </summary>
    public ServiceBusClient GetClient(string connectionString)
    {
        var key = ComputeConnectionKey(connectionString);

        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var pooled = _clients.GetOrAdd(key, k =>
            {
                Log.Debug("Creating new ServiceBusClient for connection key {Key}", k[..8]);
                return new PooledClient(new ServiceBusClient(connectionString));
            });

            pooled.ReferenceCount++;
            return pooled.Client;
        }
    }

    /// <summary>
    /// Gets or creates a ServiceBusAdministrationClient for the given connection string.
    /// </summary>
    public ServiceBusAdministrationClient GetAdminClient(string connectionString)
    {
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Admin clients are lightweight - create new ones per connection string
            return new ServiceBusAdministrationClient(connectionString);
        }
    }

    /// <summary>
    /// Releases a client reference. Idle clients remain pooled until pool disposal.
    /// </summary>
    public ValueTask ReturnClientAsync(string connectionString, ServiceBusClient client)
    {
        var key = ComputeConnectionKey(connectionString);

        lock (_lifecycleLock)
        {
            if (!_disposed &&
                _clients.TryGetValue(key, out var pooled) &&
                pooled.Client == client &&
                pooled.ReferenceCount > 0)
            {
                pooled.ReferenceCount--;
            }
        }

        // Keep idle clients pooled until pool disposal. This avoids closing a client
        // while another operations instance concurrently acquires the same entry.
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets statistics about the current pool state.
    /// </summary>
    public PoolStatistics GetStatistics()
    {
        lock (_lifecycleLock)
        {
            return new PoolStatistics(
                _clients.Count,
                _clients.Values.Sum(c => c.ReferenceCount)
            );
        }
    }

    /// <summary>
    /// Synchronously disposes all clients in the pool.
    /// Prefer DisposeAsync for proper async cleanup.
    /// </summary>
    public void Dispose()
    {
        List<KeyValuePair<string, PooledClient>> clients;
        lock (_lifecycleLock)
        {
            if (_disposed) return;
            _disposed = true;
            clients = _clients.ToList();
            _clients.Clear();
        }

        Log.Information("Disposing ServiceBusClientPool with {Count} clients", clients.Count);
        
        foreach (var kvp in clients)
        {
            try
            {
                kvp.Value.Client.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error disposing client for key {Key}", kvp.Key[..8]);
            }
        }
        
    }

    /// <summary>
    /// Asynchronously disposes all clients in the pool.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        List<KeyValuePair<string, PooledClient>> clients;
        lock (_lifecycleLock)
        {
            if (_disposed) return;
            _disposed = true;
            clients = _clients.ToList();
            _clients.Clear();
        }

        Log.Information("Disposing ServiceBusClientPool with {Count} clients", clients.Count);
        
        foreach (var kvp in clients)
        {
            try
            {
                await kvp.Value.Client.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error disposing client for key {Key}", kvp.Key[..8]);
            }
        }
        
    }

    private static string ComputeConnectionKey(string connectionString)
    {
        // Use SHA256 hash of connection string as the key
        // This protects the actual connection string from being stored in dictionary keys
        var bytes = Encoding.UTF8.GetBytes(connectionString);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private sealed class PooledClient
    {
        public ServiceBusClient Client { get; }
        public int ReferenceCount;

        public PooledClient(ServiceBusClient client)
        {
            Client = client;
        }
    }

    /// <summary>
    /// Statistics about the connection pool.
    /// </summary>
    public readonly record struct PoolStatistics(int ClientCount, int TotalReferences);
}
