namespace BusLane.Models;

public enum MessageBodyComparisonKind
{
    Json,
    Text
}

public enum MessagePropertyChangeKind
{
    Added,
    Removed,
    Modified,
    Unchanged
}

public sealed record MessageBodyComparison(
    MessageBodyComparisonKind Kind,
    bool IsChanged,
    string First,
    string Second);

public sealed record MessageFieldChange(
    string Field,
    string? FirstValue,
    string? SecondValue);

public sealed record MessagePropertyChange(
    string Key,
    MessagePropertyChangeKind Kind,
    string? FirstValue,
    string? SecondValue);

public sealed record MessageComparison(
    MessageBodyComparison Body,
    IReadOnlyList<MessageFieldChange> FieldChanges,
    IReadOnlyList<MessagePropertyChange> PropertyChanges,
    TimeSpan EnqueueTimeDelta);
