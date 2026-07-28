namespace BusLane.Services.ServiceBus;

using BusLane.Models;

public interface ICorrelationMessageCatalog
{
    event EventHandler<CorrelationCatalogChangedEventArgs>? Changed;

    void Add(CorrelationMessage message);
    void AddRange(IEnumerable<CorrelationMessage> messages);
    IReadOnlyList<CorrelationGroup> GetGroups();
    void Clear();
}

public sealed class CorrelationMessageCatalog : ICorrelationMessageCatalog
{
    private readonly int _capacity;
    private readonly object _lock = new();
    private readonly LinkedList<CorrelationMessage> _messages = [];
    private readonly Dictionary<CorrelationMessageIdentity, LinkedListNode<CorrelationMessage>> _nodes = [];

    public event EventHandler<CorrelationCatalogChangedEventArgs>? Changed;

    public CorrelationMessageCatalog(int capacity = 2_000)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
    }

    public void Add(CorrelationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        MutationResult result;
        lock (_lock)
        {
            result = AddCore(message);
        }

        RaiseChanged(result);
    }

    public void AddRange(IEnumerable<CorrelationMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var materialized = messages.ToList();
        if (materialized.Any(static message => message == null))
        {
            throw new ArgumentException("Messages cannot contain null values", nameof(messages));
        }

        var affectedGroupKeys = new HashSet<string>(StringComparer.Ordinal);
        lock (_lock)
        {
            foreach (var message in materialized)
            {
                affectedGroupKeys.UnionWith(AddCore(message).AffectedGroupKeys);
            }
        }

        if (affectedGroupKeys.Count > 0)
        {
            RaiseChanged(new MutationResult(CorrelationCatalogChangeKind.RangeAdded, affectedGroupKeys));
        }
    }

    public IReadOnlyList<CorrelationGroup> GetGroups()
    {
        CorrelationMessage[] snapshot;
        lock (_lock)
        {
            snapshot = _messages.ToArray();
        }

        return snapshot
            .Select(static message => (Message: message, Group: GetGroupIdentity(message)))
            .Where(static item => item.Group.HasValue)
            .GroupBy(static item => item.Group!.Value.Key, StringComparer.Ordinal)
            .Select(static group =>
            {
                var first = group.First().Group!.Value;
                var messages = group
                    .Select(static item => item.Message)
                    .OrderBy(static message => message.EnqueuedTime)
                    .ThenBy(static message => message.SequenceNumber)
                    .ToList();
                return new CorrelationGroup(first.Key, first.DisplayId, first.UsesSessionFallback, messages);
            })
            .OrderByDescending(static group => group.Messages[^1].EnqueuedTime)
            .ToList();
    }

    public void Clear()
    {
        HashSet<string> affectedGroupKeys;
        lock (_lock)
        {
            affectedGroupKeys = _messages
                .Select(GetGroupIdentity)
                .Where(static group => group.HasValue)
                .Select(static group => group!.Value.Key)
                .ToHashSet(StringComparer.Ordinal);
            _messages.Clear();
            _nodes.Clear();
        }

        if (affectedGroupKeys.Count > 0)
        {
            RaiseChanged(new MutationResult(CorrelationCatalogChangeKind.Cleared, affectedGroupKeys));
        }
    }

    private MutationResult AddCore(CorrelationMessage message)
    {
        var affectedGroupKeys = new HashSet<string>(StringComparer.Ordinal);
        var identity = CorrelationMessageIdentity.From(message);
        var kind = CorrelationCatalogChangeKind.Added;
        if (_nodes.Remove(identity, out var existing))
        {
            AddGroupKey(affectedGroupKeys, existing.Value);
            _messages.Remove(existing);
            kind = CorrelationCatalogChangeKind.Replaced;
        }

        AddGroupKey(affectedGroupKeys, message);
        var node = _messages.AddLast(message);
        _nodes[identity] = node;

        while (_messages.Count > _capacity)
        {
            var oldest = _messages.First!;
            _messages.RemoveFirst();
            _nodes.Remove(CorrelationMessageIdentity.From(oldest.Value));
            AddGroupKey(affectedGroupKeys, oldest.Value);
            kind = CorrelationCatalogChangeKind.Evicted;
        }

        return new MutationResult(kind, affectedGroupKeys);
    }

    private void RaiseChanged(MutationResult result)
    {
        if (result.AffectedGroupKeys.Count == 0)
        {
            return;
        }

        Changed?.Invoke(
            this,
            new CorrelationCatalogChangedEventArgs(
                result.ChangeKind,
                result.AffectedGroupKeys.ToHashSet(StringComparer.Ordinal)));
    }

    private static void AddGroupKey(ISet<string> keys, CorrelationMessage message)
    {
        var group = GetGroupIdentity(message);
        if (group.HasValue)
        {
            keys.Add(group.Value.Key);
        }
    }

    private static GroupIdentity? GetGroupIdentity(CorrelationMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.CorrelationId))
        {
            return new GroupIdentity($"corr:{message.CorrelationId}", message.CorrelationId, false);
        }

        if (!string.IsNullOrWhiteSpace(message.SessionId))
        {
            return new GroupIdentity($"session:{message.SessionId}", message.SessionId, true);
        }

        return null;
    }

    private readonly record struct GroupIdentity(string Key, string DisplayId, bool UsesSessionFallback);

    private sealed record MutationResult(
        CorrelationCatalogChangeKind ChangeKind,
        IReadOnlySet<string> AffectedGroupKeys);
}
