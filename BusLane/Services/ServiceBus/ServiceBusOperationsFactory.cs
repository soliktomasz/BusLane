namespace BusLane.Services.ServiceBus;

using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ServiceBus;
using Serilog;

/// <summary>
/// Factory for creating Service Bus operation instances.
/// </summary>
public interface IServiceBusOperationsFactory
{
    /// <summary>
    /// Creates operations using a connection string.
    /// </summary>
    IConnectionStringOperations CreateFromConnectionString(string connectionString);

    /// <summary>
    /// Creates operations using Azure credentials for a specific namespace.
    /// </summary>
    IAzureCredentialOperations CreateFromAzureCredential(string endpoint, string namespaceId, TokenCredential credential);
}

/// <summary>
/// Default implementation of the operations factory.
/// Manages connection pooling for efficient resource reuse.
/// </summary>
public class ServiceBusOperationsFactory : IServiceBusOperationsFactory, IDisposable
{
    private readonly Func<ArmClient?> _getArmClient;
    private readonly ServiceBusClientPool _clientPool;
    private bool _disposed;

    public ServiceBusOperationsFactory(Func<ArmClient?> getArmClient)
    {
        _getArmClient = getArmClient;
        _clientPool = new ServiceBusClientPool();
    }

    public IConnectionStringOperations CreateFromConnectionString(string connectionString)
    {
        Log.Debug("Creating Service Bus operations from connection string");
        return new ConnectionStringOperations(connectionString, _clientPool);
    }

    public IAzureCredentialOperations CreateFromAzureCredential(string endpoint, string namespaceId, TokenCredential credential)
    {
        var armClient = _getArmClient();
        if (armClient == null)
        {
            Log.Error("Cannot create Azure credential operations: ArmClient is not initialized");
            throw new InvalidOperationException("ArmClient is required for Azure credential operations. Please sign in first.");
        }

        Log.Debug("Creating Service Bus operations for endpoint {Endpoint} with Azure credentials", endpoint);
        return new AzureCredentialOperations(
            endpoint,
            namespaceId,
            credential,
            () => armClient.GetServiceBusNamespaceResource(new ResourceIdentifier(namespaceId))
        );
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _clientPool.Dispose();
    }
}
