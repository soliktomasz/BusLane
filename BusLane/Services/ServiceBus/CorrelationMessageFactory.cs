namespace BusLane.Services.ServiceBus;

using BusLane.Models;

public sealed record CorrelationSourceContext(
    string NamespaceName,
    ConnectionEnvironment Environment,
    string EntityName,
    string EntityType,
    string? TopicName,
    string? SubscriptionName);

public static class CorrelationMessageFactory
{
    public static CorrelationMessage FromLoaded(MessageInfo message, CorrelationSourceContext context)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        return new CorrelationMessage(
            CorrelationMessageSource.Loaded,
            context.NamespaceName,
            context.Environment,
            context.EntityName,
            context.EntityType,
            context.TopicName,
            context.SubscriptionName,
            message.MessageId,
            message.CorrelationId,
            message.SessionId,
            message.ContentType,
            message.Body,
            message.EnqueuedTime,
            message.SequenceNumber,
            CopyProperties(message.Properties),
            message.Subject,
            message.To,
            message.ReplyTo,
            message.ReplyToSessionId,
            message.PartitionKey,
            message.TimeToLive);
    }

    public static CorrelationMessage FromLiveStream(LiveStreamMessage message, CorrelationSourceContext context)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        var isSubscription = string.Equals(message.EntityType, "Subscription", StringComparison.OrdinalIgnoreCase);
        return new CorrelationMessage(
            CorrelationMessageSource.LiveStream,
            context.NamespaceName,
            context.Environment,
            message.EntityName,
            message.EntityType,
            message.TopicName,
            isSubscription ? message.EntityName : null,
            message.MessageId,
            message.CorrelationId,
            message.SessionId,
            message.ContentType,
            message.Body,
            message.EnqueuedAt,
            message.SequenceNumber,
            CopyProperties(message.Properties));
    }

    private static IReadOnlyDictionary<string, object> CopyProperties(IEnumerable<KeyValuePair<string, object>> properties)
    {
        return properties.ToDictionary(static property => property.Key, static property => property.Value);
    }
}
