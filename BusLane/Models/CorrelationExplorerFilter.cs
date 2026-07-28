namespace BusLane.Models;

public sealed record CorrelationExplorerFilter
{
    public static CorrelationExplorerFilter Empty { get; } = new();

    public string? Text { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? Namespace { get; init; }
    public string? Entity { get; init; }
    public ConnectionEnvironment? Environment { get; init; }
    public CorrelationMessageSource? Source { get; init; }
    public string? Identifier { get; init; }
    public string? PropertyKey { get; init; }
    public string? PropertyValue { get; init; }
}
