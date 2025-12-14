namespace BusLane.Models;

public class SavedMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Body { get; set; } = "";
    public string? ContentType { get; set; }
    public string? CorrelationId { get; set; }
    public string? MessageId { get; set; }
    public string? SessionId { get; set; }
    public string? Subject { get; set; }
    public string? To { get; set; }
    public string? ReplyTo { get; set; }
    public string? ReplyToSessionId { get; set; }
    public string? PartitionKey { get; set; }
    public TimeSpan? TimeToLive { get; set; }
    public DateTimeOffset? ScheduledEnqueueTime { get; set; }
    public Dictionary<string, string> CustomProperties { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CustomProperty
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

