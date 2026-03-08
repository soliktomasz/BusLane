namespace BusLane.Services.Monitoring;

using BusLane.Models;

/// <summary>
/// Persists last-reviewed inbox snapshots for delta calculations.
/// </summary>
public interface INamespaceInboxReviewStore
{
    IReadOnlyList<NamespaceInboxReviewState> LoadAll();

    NamespaceInboxReviewState? Get(string namespaceId, string entityName);

    void Save(NamespaceInboxReviewState reviewState);
}
