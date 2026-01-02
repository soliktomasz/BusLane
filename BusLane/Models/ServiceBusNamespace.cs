namespace BusLane.Models;

public record ServiceBusNamespace(
    string Id,
    string Name,
    string ResourceGroup,
    string SubscriptionId,
    string Location,
    string Endpoint
);
