namespace BusLane.Services.ServiceBus;

using BusLane.Models;

/// <summary>
/// Service for discovering Azure resources (subscriptions and Service Bus namespaces).
/// This is separate from IServiceBusOperations because it operates at the Azure level,
/// not the Service Bus namespace level.
/// </summary>
public interface IAzureResourceService
{
    /// <summary>
    /// Gets all Azure subscriptions accessible to the authenticated user.
    /// </summary>
    Task<IEnumerable<AzureSubscription>> GetAzureSubscriptionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets all Service Bus namespaces in an Azure subscription.
    /// </summary>
    Task<IEnumerable<ServiceBusNamespace>> GetNamespacesAsync(string subscriptionId, CancellationToken ct = default);
}
