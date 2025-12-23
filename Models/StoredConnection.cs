namespace BusLane.Models;

/// <summary>
/// Data transfer object for persisted connection data.
/// Connection strings are stored encrypted.
/// </summary>
internal record StoredConnection(
    string Id,
    string Name,
    string EncryptedConnectionString,
    ConnectionType Type,
    string? EntityName,
    DateTimeOffset CreatedAt,
    bool IsFavorite = false,
    ConnectionEnvironment Environment = ConnectionEnvironment.None
);

