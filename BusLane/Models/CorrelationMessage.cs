namespace BusLane.Models;

public enum CorrelationMessageSource
{
    Loaded,
    LiveStream
}

public sealed record CorrelationMessage(
    CorrelationMessageSource Source,
    string NamespaceName,
    ConnectionEnvironment Environment,
    string EntityName,
    string EntityType,
    string? TopicName,
    string? SubscriptionName,
    string MessageId,
    string? CorrelationId,
    string? SessionId,
    string? ContentType,
    string Body,
    DateTimeOffset EnqueuedTime,
    long SequenceNumber,
    IReadOnlyDictionary<string, object> Properties,
    string? Subject = null,
    string? To = null,
    string? ReplyTo = null,
    string? ReplyToSessionId = null,
    string? PartitionKey = null,
    TimeSpan? TimeToLive = null);

public sealed record CorrelationGroup(
    string Key,
    string DisplayId,
    bool UsesSessionFallback,
    IReadOnlyList<CorrelationMessage> Messages);

public readonly record struct CorrelationMessageIdentity(
    string NamespaceName,
    string EntityName,
    long SequenceNumber,
    string MessageId)
{
    public static CorrelationMessageIdentity From(CorrelationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new CorrelationMessageIdentity(
            message.NamespaceName,
            message.EntityName,
            message.SequenceNumber,
            message.MessageId);
    }
}

public enum CorrelationCatalogChangeKind
{
    Added,
    Replaced,
    Evicted,
    RangeAdded,
    Cleared
}

public sealed class CorrelationCatalogChangedEventArgs(
    CorrelationCatalogChangeKind changeKind,
    IReadOnlySet<string> affectedGroupKeys) : EventArgs
{
    public CorrelationCatalogChangeKind ChangeKind { get; } = changeKind;
    public IReadOnlySet<string> AffectedGroupKeys { get; } = affectedGroupKeys;
}
