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
    private bool _isLoaded;
    private List<NamespaceInboxReviewState> _reviews = [];

    public NamespaceInboxReviewStore(string? filePath = null)
    {
        _filePath = filePath ?? AppPaths.NamespaceInboxReviews;
    }

    public IReadOnlyList<NamespaceInboxReviewState> LoadAll()
    {
        lock (_lock)
        {
            EnsureLoaded();
            return _reviews.ToList();
        }
    }

    public NamespaceInboxReviewState? Get(string namespaceId, string entityName)
    {
        lock (_lock)
        {
            EnsureLoaded();
            return _reviews.FirstOrDefault(review =>
                string.Equals(review.NamespaceId, namespaceId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(review.EntityName, entityName, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void Save(NamespaceInboxReviewState reviewState)
    {
        ArgumentNullException.ThrowIfNull(reviewState);

        lock (_lock)
        {
            EnsureLoaded();
            var index = _reviews.FindIndex(existing =>
                string.Equals(existing.NamespaceId, reviewState.NamespaceId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.EntityName, reviewState.EntityName, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                _reviews[index] = reviewState;
            }
            else
            {
                _reviews.Add(reviewState);
            }

            SaveInternal(_reviews);
        }
    }

    private void EnsureLoaded()
    {
        if (_isLoaded)
        {
            return;
        }

        _reviews = LoadInternal();
        _isLoaded = true;
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
