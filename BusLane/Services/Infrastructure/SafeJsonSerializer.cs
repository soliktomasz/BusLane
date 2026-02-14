namespace BusLane.Services.Infrastructure;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Provides secure JSON serialization and deserialization with constraints to prevent
/// security vulnerabilities such as deserialization attacks and denial of service.
/// </summary>
public static class SafeJsonSerializer
{
    /// <summary>
    /// Maximum allowed JSON document size in bytes (10MB).
    /// </summary>
    private const long MaxDocumentSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Default deserialization options with security-hardened settings.
    /// </summary>
    private static readonly JsonSerializerOptions SecureDeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        UnknownTypeHandling = JsonUnknownTypeHandling.JsonElement,
        IncludeFields = false,
        DefaultBufferSize = 4096,
        MaxDepth = 32
    };

    /// <summary>
    /// Default serialization options with security-hardened settings.
    /// </summary>
    private static readonly JsonSerializerOptions SecureSerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultBufferSize = 4096,
        MaxDepth = 32
    };

    /// <summary>
    /// Deserializes the JSON string to the specified type with security constraints.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to. Must be a class or struct.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="options">Optional custom deserialization options.</param>
    /// <returns>The deserialized object, or default if the input is null or empty.</returns>
    /// <exception cref="ArgumentException">Thrown when the JSON exceeds the maximum document size.</exception>
    /// <exception cref="JsonException">Thrown when deserialization fails or the type is invalid.</exception>
    public static T? Deserialize<T>(string json, JsonSerializerOptions? options = null) where T : class
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        ValidateDocumentSize(json);

        var deserializeOptions = options ?? SecureDeserializeOptions;
        return JsonSerializer.Deserialize<T>(json, deserializeOptions);
    }

    /// <summary>
    /// Deserializes the JSON string to the specified value type with security constraints.
    /// </summary>
    /// <typeparam name="T">The value type to deserialize to.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="options">Optional custom deserialization options.</param>
    /// <returns>The deserialized value, or default if the input is null or empty.</returns>
    /// <exception cref="ArgumentException">Thrown when the JSON exceeds the maximum document size.</exception>
    /// <exception cref="JsonException">Thrown when deserialization fails.</exception>
    public static T? DeserializeValue<T>(string json, JsonSerializerOptions? options = null) where T : struct
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        ValidateDocumentSize(json);

        var deserializeOptions = options ?? SecureDeserializeOptions;
        return JsonSerializer.Deserialize<T>(json, deserializeOptions);
    }

    /// <summary>
    /// Deserializes the JSON string to a list of the specified type with security constraints.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list. Must be a class.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="options">Optional custom deserialization options.</param>
    /// <returns>The deserialized list, or empty list if the input is null or empty.</returns>
    /// <exception cref="ArgumentException">Thrown when the JSON exceeds the maximum document size.</exception>
    /// <exception cref="JsonException">Thrown when deserialization fails.</exception>
    public static List<T> DeserializeList<T>(string json, JsonSerializerOptions? options = null) where T : class
    {
        if (string.IsNullOrEmpty(json))
        {
            return new List<T>();
        }

        ValidateDocumentSize(json);

        var deserializeOptions = options ?? SecureDeserializeOptions;
        return JsonSerializer.Deserialize<List<T>>(json, deserializeOptions) ?? new List<T>();
    }

    /// <summary>
    /// Serializes the specified object to a JSON string with security constraints.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <param name="options">Optional custom serialization options.</param>
    /// <returns>A JSON string representation of the object.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
    /// <exception cref="JsonException">Thrown when serialization fails.</exception>
    public static string Serialize<T>(T value, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(value);

        var serializeOptions = options ?? SecureSerializeOptions;
        return JsonSerializer.Serialize(value, serializeOptions);
    }

    /// <summary>
    /// Validates that the JSON document size does not exceed the maximum allowed size.
    /// </summary>
    /// <param name="json">The JSON string to validate.</param>
    /// <exception cref="ArgumentException">Thrown when the document exceeds the maximum size.</exception>
    private static void ValidateDocumentSize(string json)
    {
        var byteCount = System.Text.Encoding.UTF8.GetByteCount(json);
        if (byteCount > MaxDocumentSizeBytes)
        {
            throw new ArgumentException(
                $"JSON document size ({byteCount} bytes) exceeds the maximum allowed size ({MaxDocumentSizeBytes} bytes).",
                nameof(json));
        }
    }
}
