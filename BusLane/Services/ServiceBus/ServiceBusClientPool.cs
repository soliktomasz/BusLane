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
    private bool _disposed;

    /// <summary>
    /// Gets or creates a ServiceBusClient for the given connection string.
    /// Multiple calls with the same connection string return the same instance.
    /// </summary>
    public ServiceBusClient GetClient(string connectionString)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var key = ComputeConnectionKey(connectionString);
        
        var pooled = _clients.GetOrAdd(key, k =>
        {
            Log.Debug("Creating new ServiceBusClient for connection key {Key}", k[..8]);
            var client = new ServiceBusClient(connectionString);
            return new PooledClient(client, 1);
        });

        Interlocked.Increment(ref pooled.ReferenceCount);
        return pooled.Client;
    }

    /// <summary>
    /// Gets or creates a ServiceBusAdministrationClient for the given connection string.
    /// </summary>
    public ServiceBusAdministrationClient GetAdminClient(string connectionString)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        // Admin clients are lightweight - create new ones per connection string
        return new ServiceBusAdministrationClient(connectionString);
    }

    /// <summary>
    /// Returns a client to the pool, disposing it asynchronously if no longer referenced.
    /// </summary>
    public async ValueTask ReturnClientAsync(string connectionString, ServiceBusClient client)
    {
        if (_disposed) return;

        var key = ComputeConnectionKey(connectionString);
        
        if (_clients.TryGetValue(key, out var pooled) && pooled.Client == client)
        {
            var newCount = Interlocked.Decrement(ref pooled.ReferenceCount);
            
            if (newCount <= 0)
            {
                // Try to remove and dispose
                if (_clients.TryRemove(key, out var removed))
                {
                    Log.Debug("Disposing pooled ServiceBusClient for key {Key}", key[..8]);
                    try
                    {
                        await removed.Client.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Error disposing client for key {Key}", key[..8]);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets statistics about the current pool state.
    /// </summary>
    public PoolStatistics GetStatistics()
    {
        return new PoolStatistics(
            _clients.Count,
            _clients.Values.Sum(c => c.ReferenceCount)
        );
    }

    /// <summary>
    /// Synchronously disposes all clients in the pool.
    /// Prefer DisposeAsync for proper async cleanup.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Log.Information("Disposing ServiceBusClientPool with {Count} clients", _clients.Count);
        
        foreach (var kvp in _clients)
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
        
        _clients.Clear();
    }

    /// <summary>
    /// Asynchronously disposes all clients in the pool.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        Log.Information("Disposing ServiceBusClientPool with {Count} clients", _clients.Count);
        
        foreach (var kvp in _clients)
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
        
        _clients.Clear();
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

        public PooledClient(ServiceBusClient client, int referenceCount)
        {
            Client = client;
            ReferenceCount = referenceCount;
        }
    }

    /// <summary>
    /// Statistics about the connection pool.
    /// </summary>
    public readonly record struct PoolStatistics(int ClientCount, int TotalReferences);
}
