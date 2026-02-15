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
)
{
    /// <summary>
    /// Gets the display subtitle for the entity.
    /// For subscriptions, shows the parent topic name.
    /// </summary>
    public string? DisplaySubtitle => Type == EntityType.Subscription && !string.IsNullOrEmpty(TopicName)
        ? $"in {TopicName}"
        : null;
};
