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
