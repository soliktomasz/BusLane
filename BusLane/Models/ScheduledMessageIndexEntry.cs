namespace BusLane.Models;

/// <summary>
/// Lifecycle state recorded for a message scheduled through BusLane.
/// </summary>
public enum ScheduledMessageRecordStatus
{
    Indexed,
    Cancelled,
    Rescheduled,
    ActionFailed,
    ResolvedLocally
}

public enum ScheduledMessageConnectionKind
{
    ConnectionString,
    AzureCredential
}

public sealed record ScheduledMessagePropertyValue(string Type, string Value)
{
    public static ScheduledMessagePropertyValue FromObject(object? value) => value switch
    {
        null => new("Null", ""),
        byte[] bytes => new(nameof(Byte) + "[]", Convert.ToBase64String(bytes)),
        char character => new(nameof(Char), character.ToString()),
        TimeSpan timeSpan => new(nameof(TimeSpan), timeSpan.ToString("c")),
        Uri uri => new(nameof(Uri), uri.ToString()),
        DateTime dateTime => new(nameof(DateTime), dateTime.ToString("O")),
        DateTimeOffset dateTimeOffset => new(nameof(DateTimeOffset), dateTimeOffset.ToString("O")),
        IFormattable formattable => new(value.GetType().Name,
            formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? ""),
        _ => new(value.GetType().Name, value.ToString() ?? "")
    };

    public object? ToObject() => Type switch
    {
        "Null" => null,
        "Byte[]" => Convert.FromBase64String(Value),
        nameof(Char) => char.Parse(Value),
        nameof(TimeSpan) => TimeSpan.ParseExact(Value, "c", System.Globalization.CultureInfo.InvariantCulture),
        nameof(Uri) => new Uri(Value, UriKind.RelativeOrAbsolute),
        nameof(Boolean) => bool.Parse(Value),
        nameof(Byte) => byte.Parse(Value, System.Globalization.CultureInfo.InvariantCulture),
        nameof(SByte) => sbyte.Parse(Value, System.Globalization.CultureInfo.InvariantCulture),
        nameof(Int16) => short.Parse(Value, System.Globalization.CultureInfo.InvariantCulture),
        nameof(Int32) => int.Parse(Value, System.Globalization.CultureInfo.InvariantCulture),
        nameof(Int64) => long.Parse(Value, System.Globalization.CultureInfo.InvariantCulture),
        nameof(UInt16) => ushort.Parse(Value, System.Globalization.CultureInfo.InvariantCulture),
        nameof(UInt32) => uint.Parse(Value, System.Globalization.CultureInfo.InvariantCulture),
        nameof(UInt64) => ulong.Parse(Value, System.Globalization.CultureInfo.InvariantCulture),
        nameof(Single) => float.Parse(Value, System.Globalization.CultureInfo.InvariantCulture),
        nameof(Double) => double.Parse(Value, System.Globalization.CultureInfo.InvariantCulture),
        nameof(Decimal) => decimal.Parse(Value, System.Globalization.CultureInfo.InvariantCulture),
        nameof(Guid) => Guid.Parse(Value),
        nameof(DateTime) => DateTime.Parse(Value, null, System.Globalization.DateTimeStyles.RoundtripKind),
        nameof(DateTimeOffset) => DateTimeOffset.Parse(Value, null, System.Globalization.DateTimeStyles.RoundtripKind),
        _ => Value
    };
}

public sealed record ScheduledMessagePayload(
    string Body,
    string? ContentType,
    string? CorrelationId,
    string? MessageId,
    string? SessionId,
    string? Subject,
    string? To,
    string? ReplyTo,
    string? ReplyToSessionId,
    string? PartitionKey,
    TimeSpan? TimeToLive,
    IReadOnlyDictionary<string, ScheduledMessagePropertyValue> Properties);

public sealed record ScheduledMessageConnectionContext(
    string ConnectionId,
    string ConnectionName,
    string NamespaceEndpoint,
    ConnectionEnvironment Environment,
    ScheduledMessageConnectionKind Kind,
    string? NamespaceResourceId = null);

/// <summary>
/// Non-sensitive local index metadata for a message scheduled through BusLane.
/// </summary>
public record ScheduledMessageIndexEntry
{
    public const int CurrentSchemaVersion = 2;

    public ScheduledMessageIndexEntry()
    {
    }

    // Retained while callers migrate and for source compatibility with the v1 model.
    public ScheduledMessageIndexEntry(
        string EntityName,
        string? SubscriptionName,
        long SequenceNumber,
        DateTimeOffset ScheduledEnqueueTime,
        string? MessageId,
        string BodyPreview,
        DateTimeOffset CreatedAt)
    {
        SchemaVersion = 1;
        RecordId = $"{EntityName}:{SequenceNumber}";
        this.EntityName = EntityName;
        this.SubscriptionName = SubscriptionName;
        this.SequenceNumber = SequenceNumber;
        this.ScheduledEnqueueTime = ScheduledEnqueueTime;
        this.MessageId = MessageId;
        this.BodyPreview = BodyPreview;
        this.CreatedAt = CreatedAt;
        UpdatedAt = CreatedAt;
    }

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string RecordId { get; init; } = Guid.NewGuid().ToString("N");
    public string? ReplacementRecordId { get; init; }
    public string ConnectionId { get; init; } = "";
    public string ConnectionName { get; init; } = "";
    public string NamespaceEndpoint { get; init; } = "";
    public string? NamespaceResourceId { get; init; }
    public ConnectionEnvironment Environment { get; init; }
    public ScheduledMessageConnectionKind ConnectionKind { get; init; }
    public string EntityName { get; init; } = "";
    public string? SubscriptionName { get; init; }
    public long SequenceNumber { get; init; }
    public DateTimeOffset ScheduledEnqueueTime { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public string? MessageId { get; init; }
    public string? CorrelationId { get; init; }
    public string? Subject { get; init; }
    public string BodyPreview { get; init; } = "";
    public IReadOnlyDictionary<string, string> SearchableProperties { get; init; } =
        new Dictionary<string, string>();
    public string? EncryptedPayload { get; init; }
    public bool IsPayloadUnavailable { get; init; }
    public ScheduledMessageRecordStatus Status { get; init; } = ScheduledMessageRecordStatus.Indexed;
    public string? LastBrokerAction { get; init; }
    public DateTimeOffset? LastBrokerActionAt { get; init; }
    public string? LastError { get; init; }

    public bool HasPayload => !IsPayloadUnavailable && !string.IsNullOrWhiteSpace(EncryptedPayload);
    public bool IsLegacyLimited => SchemaVersion < CurrentSchemaVersion || !HasPayload;
    public bool IsBrokerConfirmed =>
        Status is ScheduledMessageRecordStatus.Cancelled or ScheduledMessageRecordStatus.Rescheduled;
}
