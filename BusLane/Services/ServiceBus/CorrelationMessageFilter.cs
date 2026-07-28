namespace BusLane.Services.ServiceBus;

using System.Globalization;
using BusLane.Models;

public interface ICorrelationMessageFilter
{
    bool Matches(CorrelationMessage message, CorrelationExplorerFilter filter);
}

public sealed class CorrelationMessageFilter : ICorrelationMessageFilter
{
    public bool Matches(CorrelationMessage message, CorrelationExplorerFilter filter)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(filter);

        return MatchesText(message, filter.Text) &&
               (!filter.From.HasValue || message.EnqueuedTime >= filter.From.Value) &&
               (!filter.To.HasValue || message.EnqueuedTime <= filter.To.Value) &&
               MatchesValue(message.NamespaceName, filter.Namespace) &&
               MatchesValue(message.EntityName, filter.Entity) &&
               (!filter.Environment.HasValue || message.Environment == filter.Environment.Value) &&
               (!filter.Source.HasValue || message.Source == filter.Source.Value) &&
               MatchesIdentifier(message, filter.Identifier) &&
               MatchesProperty(message.Properties, filter.PropertyKey, filter.PropertyValue);
    }

    private static bool MatchesText(CorrelationMessage message, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        return Contains(message.CorrelationId, text) ||
               Contains(message.SessionId, text) ||
               Contains(message.MessageId, text) ||
               Contains(message.EntityName, text) ||
               Contains(message.NamespaceName, text) ||
               Contains(message.Body, text) ||
               message.Properties.Any(property =>
                   Contains(property.Key, text) ||
                   Contains(ToDisplayString(property.Value), text));
    }

    private static bool MatchesIdentifier(CorrelationMessage message, string? identifier)
    {
        return string.IsNullOrWhiteSpace(identifier) ||
               Contains(message.CorrelationId, identifier) ||
               Contains(message.SessionId, identifier);
    }

    private static bool MatchesProperty(
        IReadOnlyDictionary<string, object> properties,
        string? propertyKey,
        string? propertyValue)
    {
        if (string.IsNullOrWhiteSpace(propertyKey))
        {
            return string.IsNullOrWhiteSpace(propertyValue) ||
                   properties.Values.Any(value => Contains(ToDisplayString(value), propertyValue));
        }

        var matches = properties
            .Where(property => Contains(property.Key, propertyKey))
            .ToList();
        return matches.Count > 0 &&
               (string.IsNullOrWhiteSpace(propertyValue) ||
                matches.Any(property => Contains(ToDisplayString(property.Value), propertyValue)));
    }

    private static bool MatchesValue(string value, string? filter)
    {
        return string.IsNullOrWhiteSpace(filter) || Contains(value, filter);
    }

    private static bool Contains(string? value, string fragment)
    {
        return value?.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true;
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
}
