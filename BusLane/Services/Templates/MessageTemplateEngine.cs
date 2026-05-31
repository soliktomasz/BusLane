namespace BusLane.Services.Templates;

using System.Text.RegularExpressions;
using BusLane.Models;

public static partial class MessageTemplateEngine
{
    public static IReadOnlyList<string> ExtractTokenNames(SavedMessage message)
    {
        var tokens = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in GetTemplatedValues(message))
        {
            foreach (Match match in TokenRegex().Matches(value ?? ""))
            {
                var token = match.Groups["name"].Value.Trim();
                if (token.Length > 0 && seen.Add(token))
                {
                    tokens.Add(token);
                }
            }
        }

        return tokens;
    }

    public static IReadOnlyList<string> FindMissingTokenValues(SavedMessage message, IReadOnlyDictionary<string, string?> values)
    {
        return ExtractTokenNames(message)
            .Where(token =>
            {
                var matchingEntry = values.FirstOrDefault(kvp => string.Equals(kvp.Key, token, StringComparison.OrdinalIgnoreCase));
                return matchingEntry.Key == null || string.IsNullOrWhiteSpace(matchingEntry.Value);
            })
            .ToList();
    }

    public static SavedMessage Apply(SavedMessage message, IReadOnlyDictionary<string, string?> values)
    {
        var normalizedValues = new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase);
        var applied = message.Duplicate(message.Name);

        applied.Id = message.Id;
        applied.CreatedAt = message.CreatedAt;
        applied.Body = ReplaceTokens(message.Body, normalizedValues) ?? "";
        applied.ContentType = ReplaceTokens(message.ContentType, normalizedValues);
        applied.CorrelationId = ReplaceTokens(message.CorrelationId, normalizedValues);
        applied.MessageId = ReplaceTokens(message.MessageId, normalizedValues);
        applied.SessionId = ReplaceTokens(message.SessionId, normalizedValues);
        applied.Subject = ReplaceTokens(message.Subject, normalizedValues);
        applied.To = ReplaceTokens(message.To, normalizedValues);
        applied.ReplyTo = ReplaceTokens(message.ReplyTo, normalizedValues);
        applied.ReplyToSessionId = ReplaceTokens(message.ReplyToSessionId, normalizedValues);
        applied.PartitionKey = ReplaceTokens(message.PartitionKey, normalizedValues);
        applied.CustomProperties = message.CustomProperties.ToDictionary(
            pair => pair.Key,
            pair => ReplaceTokens(pair.Value, normalizedValues) ?? "");

        return applied;
    }

    private static IEnumerable<string?> GetTemplatedValues(SavedMessage message)
    {
        yield return message.Body;
        yield return message.ContentType;
        yield return message.CorrelationId;
        yield return message.MessageId;
        yield return message.SessionId;
        yield return message.Subject;
        yield return message.To;
        yield return message.ReplyTo;
        yield return message.ReplyToSessionId;
        yield return message.PartitionKey;

        foreach (var value in message.CustomProperties.Values)
        {
            yield return value;
        }
    }

    private static string? ReplaceTokens(string? value, IReadOnlyDictionary<string, string?> values)
    {
        if (value == null)
        {
            return null;
        }

        return TokenRegex().Replace(value, match =>
        {
            var token = match.Groups["name"].Value.Trim();
            return values.TryGetValue(token, out var replacement) ? replacement ?? "" : match.Value;
        });
    }

    [GeneratedRegex("\\{\\{(?<name>[^{}]+)\\}\\}")]
    private static partial Regex TokenRegex();
}
