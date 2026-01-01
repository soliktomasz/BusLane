namespace BusLane.Models;

public enum ConnectionType
{
    Queue,
    Topic,
    Namespace // Full namespace connection - can discover all queues/topics
}

public enum ConnectionEnvironment
{
    None,
    Development,
    Test,
    Production
}

/// <summary>
/// Represents a saved Azure Service Bus connection with validation.
/// </summary>
public record SavedConnection
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string ConnectionString { get; init; } = string.Empty;
    public ConnectionType Type { get; init; }
    public string? EntityName { get; init; } // Null for namespace-level connections
    public DateTimeOffset CreatedAt { get; init; }
    public bool IsFavorite { get; init; }
    public ConnectionEnvironment Environment { get; init; }

    /// <summary>
    /// Factory method to create a validated SavedConnection.
    /// </summary>
    /// <param name="name">Connection display name</param>
    /// <param name="connectionString">Azure Service Bus connection string</param>
    /// <param name="type">Type of connection (Queue, Topic, or Namespace)</param>
    /// <param name="entityName">Entity name (required for Queue/Topic connections)</param>
    /// <param name="environment">Environment classification (optional)</param>
    /// <param name="isFavorite">Whether to mark as favorite</param>
    /// <returns>A validated SavedConnection instance</returns>
    /// <exception cref="ArgumentException">Thrown when validation fails</exception>
    public static SavedConnection Create(
        string name,
        string connectionString,
        ConnectionType type,
        string? entityName = null,
        ConnectionEnvironment environment = ConnectionEnvironment.None,
        bool isFavorite = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString, nameof(connectionString));

        if (type != ConnectionType.Namespace && string.IsNullOrWhiteSpace(entityName))
        {
            throw new ArgumentException(
                "EntityName is required for Queue or Topic connections", nameof(entityName));
        }

        return new SavedConnection
        {
            Id = Guid.NewGuid().ToString(),
            Name = name.Trim(),
            ConnectionString = connectionString,
            Type = type,
            EntityName = entityName?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            Environment = environment,
            IsFavorite = isFavorite
        };
    }

    // Extract namespace endpoint from connection string
    public string? Endpoint
    {
        get
        {
            try
            {
                var parts = ConnectionString.Split(';');
                foreach (var part in parts)
                {
                    if (part.StartsWith("Endpoint=sb://", StringComparison.OrdinalIgnoreCase))
                    {
                        return part.Substring("Endpoint=sb://".Length).TrimEnd('/');
                    }
                }
            }
            catch { }
            return null;
        }
    }

    // Check if this is a namespace-level connection (no EntityPath in connection string)
    public bool IsNamespaceLevel => Type == ConnectionType.Namespace || string.IsNullOrEmpty(EntityName);

    // Get display name for the connection type
    public string TypeDisplayName => Type switch
    {
        ConnectionType.Queue => "Queue",
        ConnectionType.Topic => "Topic",
        ConnectionType.Namespace => "Namespace",
        _ => "Unknown"
    };

    // Get display name for the environment
    public string EnvironmentDisplayName => Environment switch
    {
        ConnectionEnvironment.Development => "DEV",
        ConnectionEnvironment.Test => "TEST",
        ConnectionEnvironment.Production => "PROD",
        _ => ""
    };

    // Check if environment is set
    public bool HasEnvironment => Environment != ConnectionEnvironment.None;
}

