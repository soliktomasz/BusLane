using Azure.ResourceManager.ServiceBus;
using Serilog;

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
        if (_auth.ArmClient == null)
        {
            Log.Warning("Cannot get Azure subscriptions: ArmClient is not initialized");
            return [];
        }

        Log.Debug("Fetching Azure subscriptions");
        var subs = new List<AzureSubscription>();
        await foreach (var sub in _auth.ArmClient.GetSubscriptions().GetAllAsync(ct))
        {
            subs.Add(new AzureSubscription(sub.Data.SubscriptionId, sub.Data.DisplayName));
        }
        Log.Information("Retrieved {Count} Azure subscriptions", subs.Count);
        return subs;
    }

    public async Task<IEnumerable<ServiceBusNamespace>> GetNamespacesAsync(string subscriptionId, CancellationToken ct = default)
    {
        if (_auth.ArmClient == null)
        {
            Log.Warning("Cannot get namespaces: ArmClient is not initialized");
            return [];
        }

        Log.Debug("Fetching Service Bus namespaces for subscription {SubscriptionId}", subscriptionId);
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
        Log.Information("Retrieved {Count} Service Bus namespaces in subscription {SubscriptionId}", 
            namespaces.Count, subscriptionId);
        return namespaces;
    }
}
