namespace BusLane.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Identifies a pinned Service Bus entity within a saved workspace.
/// </summary>
public record PinnedEntity(
    string WorkspaceId,
    PinnedEntityType Type,
    string Name,
    string? TopicName)
{
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
