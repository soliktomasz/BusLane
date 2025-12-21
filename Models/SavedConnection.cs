namespace BusLane.Models;

public enum ConnectionType
{
    Queue,
    Topic,
    Namespace // Full namespace connection - can discover all queues/topics
}

public record SavedConnection(
    string Id,
    string Name,
    string ConnectionString,
    ConnectionType Type,
    string? EntityName, // Null for namespace-level connections
    DateTimeOffset CreatedAt,
    bool IsFavorite = false
)
{
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
}

