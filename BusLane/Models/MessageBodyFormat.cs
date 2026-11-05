namespace BusLane.Models;

/// <summary>
/// Detects and pretty-prints message bodies for read-only inspectors.
/// </summary>
public static class MessageBodyFormat
{
    public static bool IsJson(string? body)
    {
        if (body == null) return false;
        var trimmed = body.Trim();
        return (trimmed.StartsWith('{') && trimmed.EndsWith('}')) ||
               (trimmed.StartsWith('[') && trimmed.EndsWith(']'));
    }

    public static bool IsXml(string? body)
    {
        if (body == null) return false;
        var trimmed = body.Trim();
        return trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
               (trimmed.StartsWith('<') && trimmed.Contains("</") && trimmed.EndsWith('>'));
    }

    /// <summary>
    /// Returns indented JSON when the body parses as JSON, otherwise the body unchanged.
    /// </summary>
    public static string? Format(string? body)
    {
        if (body == null || !IsJson(body)) return body;

        try
        {
            using var jsonDoc = System.Text.Json.JsonDocument.Parse(body);
            return System.Text.Json.JsonSerializer.Serialize(jsonDoc.RootElement,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return body;
        }
    }
}
