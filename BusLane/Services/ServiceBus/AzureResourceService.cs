using Azure.ResourceManager.ServiceBus;

namespace BusLane.Services.ServiceBus;

using BusLane.Models;
using BusLane.Services.Auth;

/// <summary>
/// Implementation of Azure resource discovery using the Azure auth service.
/// </summary>
public class AzureResourceService : IAzureResourceService
{
    private readonly IAzureAuthService _auth;

    public AzureResourceService(IAzureAuthService auth)
    {
        _auth = auth;
    }

    public async Task<IEnumerable<AzureSubscription>> GetAzureSubscriptionsAsync(CancellationToken ct = default)
    {
        if (_auth.ArmClient == null) return [];

        var subs = new List<AzureSubscription>();
        await foreach (var sub in _auth.ArmClient.GetSubscriptions().GetAllAsync(ct))
        {
            subs.Add(new AzureSubscription(sub.Data.SubscriptionId, sub.Data.DisplayName));
        }
        return subs;
    }

    public async Task<IEnumerable<ServiceBusNamespace>> GetNamespacesAsync(string subscriptionId, CancellationToken ct = default)
    {
        if (_auth.ArmClient == null) return [];

        var sub = _auth.ArmClient.GetSubscriptionResource(
            new Azure.Core.ResourceIdentifier($"/subscriptions/{subscriptionId}")
        );

        var namespaces = new List<ServiceBusNamespace>();
        await foreach (var ns in sub.GetServiceBusNamespacesAsync(ct))
        {
            var endpoint = $"{ns.Data.Name}.servicebus.windows.net";

            namespaces.Add(new ServiceBusNamespace(
                ns.Id.ToString(),
                ns.Data.Name,
                ns.Id.ResourceGroupName ?? "",
                subscriptionId,
                ns.Data.Location.DisplayName ?? "",
                endpoint
            ));
        }
        return namespaces;
    }
}
