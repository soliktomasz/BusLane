namespace BusLane.Services.ServiceBus;

using System.Globalization;
using System.Text;
using System.Text.Json;
using BusLane.Models;

public interface ICorrelationMessageComparisonService
{
    MessageComparison Compare(CorrelationMessage first, CorrelationMessage second);
}

public sealed class CorrelationMessageComparisonService : ICorrelationMessageComparisonService
{
    public MessageComparison Compare(CorrelationMessage first, CorrelationMessage second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return new MessageComparison(
            CompareBodies(first.Body, second.Body),
            CompareFields(first, second),
            CompareProperties(first.Properties, second.Properties),
            second.EnqueuedTime - first.EnqueuedTime);
    }

    private static MessageBodyComparison CompareBodies(string first, string second)
    {
        if (TryNormalizeJson(first, out var normalizedFirst) &&
            TryNormalizeJson(second, out var normalizedSecond))
        {
            return new MessageBodyComparison(
                MessageBodyComparisonKind.Json,
                !string.Equals(normalizedFirst, normalizedSecond, StringComparison.Ordinal),
                normalizedFirst,
                normalizedSecond);
        }

        return new MessageBodyComparison(
            MessageBodyComparisonKind.Text,
            !string.Equals(first, second, StringComparison.Ordinal),
            first,
            second);
    }

    private static IReadOnlyList<MessageFieldChange> CompareFields(
        CorrelationMessage first,
        CorrelationMessage second)
    {
        var changes = new List<MessageFieldChange>();
        AddChange(changes, "Namespace", first.NamespaceName, second.NamespaceName);
        AddChange(changes, "Environment", first.Environment.ToString(), second.Environment.ToString());
        AddChange(changes, "Entity", first.EntityName, second.EntityName);
        AddChange(changes, "EntityType", first.EntityType, second.EntityType);
        AddChange(changes, "Topic", first.TopicName, second.TopicName);
        AddChange(changes, "Subscription", first.SubscriptionName, second.SubscriptionName);
        AddChange(changes, "Source", first.Source.ToString(), second.Source.ToString());
        AddChange(changes, "MessageId", first.MessageId, second.MessageId);
        AddChange(changes, "CorrelationId", first.CorrelationId, second.CorrelationId);
        AddChange(changes, "SessionId", first.SessionId, second.SessionId);
        AddChange(changes, "ContentType", first.ContentType, second.ContentType);
        AddChange(changes, "Subject", first.Subject, second.Subject);
        AddChange(changes, "To", first.To, second.To);
        AddChange(changes, "ReplyTo", first.ReplyTo, second.ReplyTo);
        AddChange(changes, "ReplyToSessionId", first.ReplyToSessionId, second.ReplyToSessionId);
        AddChange(changes, "PartitionKey", first.PartitionKey, second.PartitionKey);
        AddChange(changes, "TimeToLive", first.TimeToLive?.ToString(), second.TimeToLive?.ToString());
        AddChange(
            changes,
            "SequenceNumber",
            first.SequenceNumber.ToString(CultureInfo.InvariantCulture),
            second.SequenceNumber.ToString(CultureInfo.InvariantCulture));
        return changes;
    }

    private static IReadOnlyList<MessagePropertyChange> CompareProperties(
        IReadOnlyDictionary<string, object> first,
        IReadOnlyDictionary<string, object> second)
    {
        var keys = first.Keys
            .Concat(second.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static key => key, StringComparer.Ordinal);
        var changes = new List<MessagePropertyChange>();

        foreach (var key in keys)
        {
            var hasFirst = first.TryGetValue(key, out var firstValue);
            var hasSecond = second.TryGetValue(key, out var secondValue);
            var kind = (hasFirst, hasSecond) switch
            {
                (false, true) => MessagePropertyChangeKind.Added,
                (true, false) => MessagePropertyChangeKind.Removed,
                _ when Equals(firstValue, secondValue) => MessagePropertyChangeKind.Unchanged,
                _ => MessagePropertyChangeKind.Modified
            };

            changes.Add(new MessagePropertyChange(
                key,
                kind,
                hasFirst ? ToDisplayString(firstValue) : null,
                hasSecond ? ToDisplayString(secondValue) : null));
        }

        return changes;
    }

    private static string? ToDisplayString(object? value)
    {
        return value switch
        {
            null => null,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    private static void AddChange(
        ICollection<MessageFieldChange> changes,
        string field,
        string? first,
        string? second)
    {
        if (!string.Equals(first, second, StringComparison.Ordinal))
        {
            changes.Add(new MessageFieldChange(field, first, second));
        }
    }

    private static bool TryNormalizeJson(string value, out string normalized)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                WriteCanonical(writer, document.RootElement);
            }

            normalized = Encoding.UTF8.GetString(stream.ToArray());
            return true;
        }
        catch (JsonException)
        {
            normalized = value;
            return false;
        }
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(
                             static property => property.Name,
                             StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
