namespace BusLane.Models.Dashboard;

public enum EntityType
{
    Queue,
    Topic,
    Subscription
}

public record TopEntityInfo(
    string Name,
    long MessageCount,
    double PercentageOfTotal,
    EntityType Type,
    string? TopicName = null
);
