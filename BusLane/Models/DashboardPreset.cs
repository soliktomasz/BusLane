namespace BusLane.Models;

/// <summary>
/// Named dashboard preset bound to a namespace or connection context.
/// </summary>
public record DashboardPreset(
    string Id,
    string Name,
    string? NamespaceId,
    string ConnectionKind,
    DashboardConfiguration Configuration);
