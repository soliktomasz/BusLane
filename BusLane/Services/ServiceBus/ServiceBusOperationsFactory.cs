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
    private readonly Func<ArmClient?> _getArmClient;

    public ServiceBusOperationsFactory(Func<ArmClient?> getArmClient)
    {
        _getArmClient = getArmClient;
    }

    public IConnectionStringOperations CreateFromConnectionString(string connectionString)
    {
        return new ConnectionStringOperations(connectionString);
    }

    public IAzureCredentialOperations CreateFromAzureCredential(string endpoint, string namespaceId, TokenCredential credential)
    {
        var armClient = _getArmClient();
        if (armClient == null)
            throw new InvalidOperationException("ArmClient is required for Azure credential operations. Please sign in first.");

        return new AzureCredentialOperations(
            endpoint,
            namespaceId,
            credential,
            () => armClient.GetServiceBusNamespaceResource(new ResourceIdentifier(namespaceId))
        );
    }
}
