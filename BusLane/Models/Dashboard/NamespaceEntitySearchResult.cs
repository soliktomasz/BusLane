namespace BusLane.Models.Dashboard;

/// <summary>Searchable namespace entity and its default destination.</summary>
public sealed record NamespaceEntitySearchResult(
    string EntityName,
    string DisplayPath,
    string TypeLabel,
    NamespaceNavigationRequest Request);
