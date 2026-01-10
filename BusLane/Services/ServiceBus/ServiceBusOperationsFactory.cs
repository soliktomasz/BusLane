namespace BusLane.Services.ServiceBus;

using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ServiceBus;

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
/// </summary>
public class ServiceBusOperationsFactory : IServiceBusOperationsFactory
{
    private readonly ArmClient? _armClient;

    public ServiceBusOperationsFactory(ArmClient? armClient = null)
    {
        _armClient = armClient;
    }

    public IConnectionStringOperations CreateFromConnectionString(string connectionString)
    {
        return new ConnectionStringOperations(connectionString);
    }

    public IAzureCredentialOperations CreateFromAzureCredential(string endpoint, string namespaceId, TokenCredential credential)
    {
        if (_armClient == null)
            throw new InvalidOperationException("ArmClient is required for Azure credential operations");

        return new AzureCredentialOperations(
            endpoint,
            namespaceId,
            credential,
            () => _armClient.GetServiceBusNamespaceResource(new ResourceIdentifier(namespaceId))
        );
    }
}
