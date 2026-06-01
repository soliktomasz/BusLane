namespace BusLane.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Identifies a pinned Service Bus entity within a saved workspace.
/// </summary>
public record PinnedEntity
{
    public PinnedEntity(
        string workspaceId,
        PinnedEntityType type,
        string name,
        string? topicName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (type == PinnedEntityType.Subscription && string.IsNullOrWhiteSpace(topicName))
        {
            throw new ArgumentException("TopicName is required for subscription pins.", nameof(topicName));
        }

        if (type != PinnedEntityType.Subscription && !string.IsNullOrWhiteSpace(topicName))
        {
            throw new ArgumentException("TopicName is only valid for subscription pins.", nameof(topicName));
        }

        WorkspaceId = workspaceId;
        Type = type;
        Name = name;
        TopicName = topicName;
    }

    public string WorkspaceId { get; init; }
    public PinnedEntityType Type { get; init; }
    public string Name { get; init; }
    public string? TopicName { get; init; }

    public string DisplayName => Type == PinnedEntityType.Subscription && !string.IsNullOrWhiteSpace(TopicName)
        ? $"{TopicName}/{Name}"
        : Name;

    public string TypeLabel => Type.ToString();
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PinnedEntityType
{
    Queue,
    Topic,
    Subscription
}
