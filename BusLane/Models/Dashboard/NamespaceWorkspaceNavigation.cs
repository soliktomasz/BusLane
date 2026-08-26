namespace BusLane.Models.Dashboard;

/// <summary>Workspace displayed inside one connected namespace tab.</summary>
public enum NamespaceWorkspaceMode
{
    Overview,
    Entity
}

/// <summary>Section last displayed inside namespace Overview.</summary>
public enum NamespaceOverviewSection
{
    Home,
    Issues,
    Analytics
}

/// <summary>Meaningful destination inside entity workspace.</summary>
public enum EntityWorkspaceView
{
    ActiveMessages,
    DeadLetters,
    Sessions,
    TopicSubscriptions
}

/// <summary>Typed request for navigation within current namespace tab.</summary>
public sealed record NamespaceNavigationRequest(
    EntityType EntityType,
    string EntityName,
    string? TopicName,
    EntityWorkspaceView View);

/// <summary>Successfully opened entity destination retained for quick return.</summary>
public sealed record RecentEntityDestination(
    NamespaceNavigationRequest Request,
    DateTimeOffset OpenedAt);
