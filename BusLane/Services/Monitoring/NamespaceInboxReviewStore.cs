namespace BusLane.Services.Monitoring;

using BusLane.Models;
using BusLane.Services.Infrastructure;
using static BusLane.Services.Infrastructure.SafeJsonSerializer;

/// <summary>
/// JSON-backed storage for namespace inbox review state.
/// </summary>
public class NamespaceInboxReviewStore : INamespaceInboxReviewStore
{
    private readonly object _lock = new();
    private readonly string _filePath;

    public NamespaceInboxReviewStore(string? filePath = null)
    {
        _filePath = filePath ?? AppPaths.NamespaceInboxReviews;
    }

    public IReadOnlyList<NamespaceInboxReviewState> LoadAll()
    {
        lock (_lock)
        {
            return LoadInternal();
        }
    }

    public NamespaceInboxReviewState? Get(string namespaceId, string entityName)
    {
        lock (_lock)
        {
            return LoadInternal().FirstOrDefault(review =>
                string.Equals(review.NamespaceId, namespaceId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(review.EntityName, entityName, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void Save(NamespaceInboxReviewState reviewState)
    {
        ArgumentNullException.ThrowIfNull(reviewState);

        lock (_lock)
        {
            var reviews = LoadInternal();
            var index = reviews.FindIndex(existing =>
                string.Equals(existing.NamespaceId, reviewState.NamespaceId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.EntityName, reviewState.EntityName, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                reviews[index] = reviewState;
            }
            else
            {
                reviews.Add(reviewState);
            }

            SaveInternal(reviews);
        }
    }

    private List<NamespaceInboxReviewState> LoadInternal()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return [];
            }

            var json = File.ReadAllText(_filePath);
            return Deserialize<List<NamespaceInboxReviewState>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void SaveInternal(List<NamespaceInboxReviewState> reviews)
    {
        var json = Serialize(reviews);
        AppPaths.CreateSecureFile(_filePath, json);
    }
}
